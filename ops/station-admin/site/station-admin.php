<?php
declare(strict_types=1);
require_once __DIR__.'/station-lib.php';
require_once __DIR__.'/station-admin-policy.php';
$admin=tb_require_user('admin');
header('Cache-Control: no-store, no-cache, must-revalidate, max-age=0');
header('Pragma: no-cache');
header('Referrer-Policy: no-referrer');
$db=tb_db();
$wantsJson=($_GET['format'] ?? '')==='json';
function station_json(int $status,array $data): never {
    http_response_code($status);header('Content-Type: application/json; charset=utf-8');
    echo json_encode($data,JSON_THROW_ON_ERROR|JSON_UNESCAPED_UNICODE);exit;
}
function station_support(string $id): array {
    [$status,$body]=tb_station_admin_request('GET','/management/licenses/'.rawurlencode($id).'/support');
    if($status!==200) throw new RuntimeException('Cadastro Station indisponível.');
    $body['license']['allowedActions']=tb_station_actions($body['license']);
    $body['license']['presentation']=tb_station_status($body['license']);
    return $body;
}
if($_SERVER['REQUEST_METHOD']==='POST') {
    tb_check_csrf();
    $id=strtoupper(trim((string)($_POST['license_id'] ?? '')));
    $action=(string)($_POST['action'] ?? '');
    $reason=trim((string)($_POST['reason'] ?? ''));
    $req=(string)($_POST['request_id'] ?? '');
    if(!preg_match('/^STA-[A-Z0-9_-]{6,64}$/',$id)||!isset(tb_station_action_info()[$action])||
       !preg_match('/^[a-z0-9]{32}$/',$req)||mb_strlen($reason)<10||mb_strlen($reason)>200||preg_match('/[\x00-\x1f]/u',$reason))
        station_json(400,['ok'=>false,'message'=>'Confira a ação e informe um motivo de 10 a 200 caracteres.']);
    if(!tb_rate_limit('station_manage_admin_'.(int)$admin['id'],20,900))
        station_json(429,['ok'=>false,'message'=>'Muitas tentativas. Aguarde antes de continuar.']);
    $verify=$db->prepare("SELECT password_hash FROM users WHERE id=? AND role='admin' AND status='active'");
    $verify->execute([(int)$admin['id']]);
    if(!password_verify((string)($_POST['password'] ?? ''),(string)$verify->fetchColumn()))
        station_json(403,['ok'=>false,'message'=>'Senha administrativa incorreta. A alteração não foi enviada.']);
    $generation=filter_var($_POST['generation'] ?? '',FILTER_VALIDATE_INT,['options'=>['min_range'=>0]]);
    $activation=filter_var($_POST['activation_generation'] ?? '',FILTER_VALIDATE_INT,['options'=>['min_range'=>0]]);
    if($generation===false||$activation===false)station_json(400,['ok'=>false,'message'=>'Atualize o cadastro para continuar.']);
    try {
        $current=station_support($id)['license'];
        if((int)$current['revocationGeneration']!==$generation||(int)$current['activationGeneration']!==$activation)
            station_json(409,['ok'=>false,'message'=>tb_station_failure('STATION_STATE_CHANGED')]);
        if(!in_array($action,$current['allowedActions'],true))
            station_json(409,['ok'=>false,'message'=>'Essa ação não está disponível no estado atual. Atualize o cadastro.']);
        if($action==='revoke-session' && (string)($_POST['session_id'] ?? '')!==(string)$current['sessionId'])
            station_json(409,['ok'=>false,'message'=>tb_station_failure('STATION_STATE_CHANGED')]);
        $body=['actor'=>'turbobox-admin-'.(int)$admin['id'],'requestId'=>$req,
            'expectedGeneration'=>$generation,'expectedActivationGeneration'=>$activation,
            'reason'=>$reason,'stepUpAt'=>time(),'clientIpDigest'=>hash('sha256','station-admin|'.tb_client_ip()),
            'csrfVerified'=>true,'targetSessionId'=>$action==='revoke-session'?(string)$current['sessionId']:null];
        [$status,$result]=tb_station_admin_request('POST','/management/licenses/'.rawurlencode($id).'/'.$action,$body);
        if($status!==200)station_json($status,['ok'=>false,'partial'=>!empty($result['transferCompleted']),
            'message'=>tb_station_failure((string)($result['code'] ?? ''))]);
        $code=(string)($result['activationCode'] ?? '');
        if($code!=='' && !preg_match('/^[A-Za-z0-9_-]{43}$/',$code))
            station_json(502,['ok'=>false,'message'=>'A ação foi enviada, mas o código não pôde ser conferido. Confira o histórico.']);
        // License audit is already committed in PostgreSQL. A site-audit failure must not hide a issued code.
        try {tb_audit($db,(int)$admin['id'],'station_admin_'.$action,'station_license',null,$id);} catch(Throwable) {}
        $queued=false;$deliveryMessage='';
        if($code!=='' && isset($_POST['enviar_whatsapp'])) {
            try {
                $recipient=tb_station_resolve_recipient($db,$id,(string)($_POST['phone'] ?? ''),$current);
                if(!tb_station_is_test_license($current)) {
                    $queued=tb_station_queue_issue_whatsapp($db,['phone'=>$recipient['phone'],'userId'=>$recipient['userId'],
                        'name'=>$recipient['name'],'license'=>$id,'customerRef'=>(string)$current['customerRef'],
                        'code'=>$code,'ttl'=>'30 minutos','expires'=>tb_station_format_expiry((string)$result['expiresAt']),
                        'order'=>(string)$current['sourcePurchaseId'],'sku'=>(string)$current['sourceItemKey'],'firstIssue'=>false]);
                }
                $deliveryMessage=$queued?'Aviso colocado na fila do WhatsApp.':'WhatsApp não enviado. Entregue o código manualmente.';
            } catch(Throwable) {$deliveryMessage='Não foi possível confirmar a fila do WhatsApp. Entregue o código manualmente.';}
        }
        $response=['ok'=>true,'action'=>$action,'message'=>$code!==''?'Operação concluída. Entregue o novo código ao cliente.':'Operação concluída. Atualize o cadastro para conferir o estado.'];
        if($code!=='')$response['issued']=['code'=>$code,'expires'=>tb_station_format_expiry((string)$result['expiresAt']),
            'expiresAt'=>(string)$result['expiresAt'],'licenseId'=>$id,'delivery'=>$deliveryMessage?:'WhatsApp não solicitado. Copie o código e entregue por canal privado.'];
        station_json(200,$response);
    } catch(Throwable) {station_json(503,['ok'=>false,'message'=>'Atendimento Station indisponível. Confira o estado e o histórico antes de repetir a ação.']);}
}
$selected=strtoupper(trim((string)($_GET['license'] ?? '')));
if($wantsJson && $selected!=='') {
    if(!preg_match('/^STA-[A-Z0-9_-]{6,64}$/',$selected))station_json(400,['ok'=>false,'message'=>'Licença inválida.']);
    try {station_json(200,station_support($selected));} catch(Throwable) {station_json(503,['ok'=>false,'message'=>'Não foi possível consultar o atendimento.']);}
}
$q=mb_substr(trim((string)($_GET['q'] ?? '')),0,128);
$state=in_array($_GET['state'] ?? 'all',['all','active','pending','suspended','attention'],true)?(string)($_GET['state'] ?? 'all'):'all';
$page=max(1,min(50000,(int)($_GET['page'] ?? 1)));$limit=20;$offset=($page-1)*$limit;
$list=['licenses'=>[],'total'=>0,'summary'=>[]];$listError='';
try {
    [$status,$data]=tb_station_admin_request('GET','/management/licenses?'.http_build_query(['q'=>$q,'state'=>$state,'limit'=>$limit,'offset'=>$offset]));
    if($status!==200)throw new RuntimeException();$list=$data;
} catch(Throwable) {$listError='Gestão Station indisponível no momento. Nenhuma alteração foi enviada.';}
$actions=tb_station_action_info();
?>
<!doctype html><html lang="pt-BR"><head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="robots" content="noindex">
<title>Station · Licenças e aparelhos</title><link rel="stylesheet" href="/payments.css?v=8"><link rel="stylesheet" href="/station-admin.css?v=20261003-2">
</head><body>
<aside><a class="brand" href="/admin">TURBO<b>BOX</b></a><small>ADMINISTRAÇÃO</small><nav aria-label="Administração">
<?php foreach(['/admin'=>'Visão geral','/admin/clientes'=>'Clientes','/admin/produtos'=>'Produtos','/admin/vendas'=>'Vendas','/admin/pagamentos'=>'Pagamentos','/admin/station'=>'Station','/admin/conteudos'=>'Conteúdos','/admin/seguranca'=>'Segurança'] as $href=>$label): ?><a href="<?=tb_e($href)?>" <?=$href==='/admin/station'?'class="active" aria-current="page"':''?>><?=tb_e($label)?></a><?php endforeach;?>
</nav></aside>
<main data-action-info="<?=tb_e(json_encode($actions,JSON_THROW_ON_ERROR|JSON_UNESCAPED_UNICODE))?>"><header class="station-heading"><div><small>TURBORAMA STATION</small><h1>Licenças e aparelhos</h1><p>Resolva a ativação, a reinstalação e a troca de celular em um só atendimento.</p></div><span class="live">1 aparelho por licença</span></header>
<div id="station-notice" class="notice station-hidden" role="status" aria-live="polite"></div>
<?php if($listError):?><div class="notice cleanup-error" role="alert"><?=tb_e($listError)?></div><?php endif;?>
<section class="station-stats" aria-label="Resumo de licenças">
<?php foreach(['total'=>'Licenças','active'=>'Aparelhos ativados','pending'=>'Aguardando ativação','suspended'=>'Acessos bloqueados'] as $key=>$label):?><article><span><?=tb_e($label)?></span><strong><?=(int)($list['summary'][$key]??0)?></strong></article><?php endforeach;?>
</section>
<section class="station-help"><div><span class="station-help-icon" aria-hidden="true">?</span><div><h2>Qual é a situação do cliente?</h2><p>Abra o atendimento da licença. O painel mostra as ações possíveis e explica o efeito antes de confirmar.</p></div></div><details><summary>Guia rápido de atendimento</summary><dl><dt>Código venceu ou foi perdido</dt><dd>Gere outro código. O anterior deixa de funcionar.</dd><dt>Reinstalou o aplicativo</dt><dd>Use a ação de reinstalação para liberar o vínculo anterior e emitir novo código.</dd><dt>Comprou outro celular</dt><dd>Troque o aparelho. O anterior perde o acesso antes da nova ativação.</dd><dt>Perdeu o celular ou precisa suspender o acesso</dt><dd>Bloqueie a licença. O acesso só retorna depois do desbloqueio autorizado.</dd><dt>Pagamento pendente</dt><dd>Confira a venda em Pagamentos. O painel não libera acesso sem pagamento confirmado.</dd></dl></details></section>
<section class="card station-list"><div class="title"><div><small>ATENDIMENTO</small><h2>Clientes Station</h2></div><span><?=(int)$list['total']?> resultado(s)</span></div>
<form method="get" class="station-filters"><label><span>Buscar cliente ou licença</span><input name="q" value="<?=tb_e($q)?>" placeholder="Nome, licença ou pedido" maxlength="128"></label><label><span>Situação</span><select name="state"><?php foreach(['all'=>'Todas as situações','active'=>'Aparelho ativado','pending'=>'Aguardando ativação','suspended'=>'Acesso bloqueado','attention'=>'Precisa de atenção'] as $key=>$label):?><option value="<?=tb_e($key)?>" <?=$state===$key?'selected':''?>><?=tb_e($label)?></option><?php endforeach;?></select></label><button type="submit" class="station-primary">Buscar</button><a href="/admin/station" class="station-button">Limpar</a></form>
<div class="table-wrap"><table><thead><tr><th>Cliente / licença</th><th>Situação</th><th>Aparelho</th><th>Ativação</th><th>Atendimento</th></tr></thead><tbody>
<?php foreach($list['licenses'] as $r): $id=(string)$r['licenseId'];[$type,$label,$hint]=tb_station_status($r);?><tr><td><strong><?=tb_e(trim((string)$r['displayName'])?:'Cliente Station')?></strong><small><?=tb_e(tb_station_mask_license($id))?></small><small>Pedido <?=tb_e((string)$r['sourcePurchaseId'])?></small></td><td><span class="station-badge station-<?=tb_e($type)?>"><?=tb_e($label)?></span></td><td><?php if(!empty($r['deviceLinked'])):?><strong><?=tb_e(trim((string)$r['manufacturer'].' '.(string)$r['model']))?></strong><small>Vínculo ativo</small><?php else:?><span class="station-muted">Nenhum aparelho vinculado</span><?php endif;?></td><td><?php if(!empty($r['activationConsumed'])):?><span>Ativação concluída</span><?php elseif(!empty($r['codeValid'])):?><span>Código emitido</span><small>Até <?=tb_e(tb_station_format_expiry((string)$r['activationExpiresAt']))?></small><?php elseif(!empty($r['codeIssued'])):?><span>Código vencido</span><?php else:?><span class="station-muted">Aguardando emissão</span><?php endif;?></td><td><button type="button" class="station-button" data-support="<?=tb_e($id)?>">Abrir atendimento <span aria-hidden="true">→</span></button></td></tr><?php endforeach;?>
<?php if(!$list['licenses']):?><tr><td colspan="5" class="empty"><?=$listError?'O serviço não pôde ser consultado.':'Nenhuma licença corresponde à busca.'?></td></tr><?php endif;?></tbody></table></div>
<div class="station-pagination"><span>Página <?=$page?> · até 20 licenças por página</span><div><?php if($page>1):?><a class="station-button" href="?<?=tb_e(http_build_query(['q'=>$q,'state'=>$state,'page'=>$page-1]))?>">Anterior</a><?php endif;?><?php if($offset+$limit<(int)$list['total']):?><a class="station-button" href="?<?=tb_e(http_build_query(['q'=>$q,'state'=>$state,'page'=>$page+1]))?>">Próxima</a><?php endif;?></div></div></section>
<noscript><p class="notice cleanup-error">Ative o JavaScript do navegador para abrir o atendimento e confirmar alterações.</p></noscript>
</main>
<dialog id="station-support" class="station-dialog" aria-labelledby="station-support-title"><div class="station-dialog-head"><div><small>ATENDIMENTO STATION</small><h2 id="station-support-title">Consultar licença</h2></div><button type="button" data-close="station-support" class="station-close" aria-label="Fechar atendimento">×</button></div><div id="station-support-body" class="station-dialog-body"></div></dialog>
<dialog id="station-confirm" class="station-dialog station-confirm" aria-labelledby="station-confirm-title"><form id="station-action-form"><div class="station-dialog-head"><div><small>CONFIRMAÇÃO DE ATENDIMENTO</small><h2 id="station-confirm-title">Confirmar ação</h2></div><button type="button" data-close="station-confirm" class="station-close" aria-label="Cancelar">×</button></div><div class="station-dialog-body"><p id="station-confirm-client" class="station-client"></p><div id="station-effect" class="station-effect"></div><label class="station-field">Motivo do atendimento<textarea name="reason" id="station-reason" minlength="10" maxlength="200" required rows="3"></textarea></label><div id="station-delivery-options" class="station-hidden"><label class="station-field">WhatsApp do cliente <span class="station-muted">(opcional)</span><input name="phone" type="tel" autocomplete="tel" placeholder="DDD e número"></label><label class="station-check"><input name="enviar_whatsapp" type="checkbox" value="1"> Confirmar envio do novo código pelo WhatsApp</label></div><label class="station-field">Sua senha administrativa<input name="password" type="password" autocomplete="current-password" required></label><p id="station-action-error" class="station-inline-error station-hidden" role="alert"></p><input type="hidden" name="csrf" value="<?=tb_e(tb_csrf())?>"><input type="hidden" name="license_id"><input type="hidden" name="action"><input type="hidden" name="generation"><input type="hidden" name="activation_generation"><input type="hidden" name="request_id"><input type="hidden" name="session_id"><div class="station-dialog-actions"><button type="button" data-close="station-confirm" class="station-button">Cancelar</button><button type="submit" id="station-submit" class="station-primary">Confirmar ação</button></div></div></form></dialog>
<dialog id="station-issued" class="station-dialog station-issued" aria-labelledby="station-issued-title"><div class="station-dialog-head"><div><small>OPERAÇÃO CONCLUÍDA</small><h2 id="station-issued-title">Novo código de ativação</h2></div><button type="button" data-close="station-issued" class="station-close" aria-label="Fechar e ocultar código">×</button></div><div class="station-dialog-body"><p>Entregue este código ao cliente para preencher o login do aplicativo. Ele aparece uma única vez e vale 30 minutos.</p><code id="station-code" class="station-code"></code><button type="button" id="station-copy-code" class="station-primary">Copiar código</button><p id="station-code-expiry"></p><p id="station-code-delivery" class="station-muted"></p><p class="station-effect">Ao fechar, sair ou atualizar a página, o código será ocultado. O código anterior já deixou de funcionar.</p><button type="button" data-close="station-issued" class="station-button">Fechar e atualizar cadastro</button></div></dialog>
<script src="/station-admin.js?v=20261003-2" defer></script></body></html>
