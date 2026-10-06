<?php
declare(strict_types=1);

function tb_station_status(array $r): array {
    if (empty($r['eligibleDelivery'])) {
        if (($r['financialState'] ?? '') === 'PAID') return ['attention','Liberação pendente','O pagamento consta como confirmado, mas a entrega da licença precisa ser conferida em Vendas.'];
        return ['attention','Pagamento não confirmado','Confira o pagamento em Vendas. Acesso não pode ser liberado por esta página.'];
    }
    if (($r['status'] ?? '') === 'SUSPENDED') return ['suspended','Acesso bloqueado','O aplicativo não pode abrir uma sessão até o desbloqueio.'];
    if (($r['status'] ?? '') !== 'ACTIVE') return ['attention','Licença indisponível','Confira o cadastro e o histórico antes de alterar a licença.'];
    if (!empty($r['activationConsumed']) || ($r['enrollmentState'] ?? '') === 'BOUND') return ['active','Aparelho ativado','A licença está vinculada ao aparelho. Reinstalação ou troca de celular exige liberar a ativação.'];
    if (!empty($r['codeValid'])) return ['pending','Aguardando ativação','Entregue o código já emitido ao cliente. Se precisar de outro, o anterior será invalidado.'];
    if (!empty($r['codeIssued'])) return ['pending','Código vencido','Gere um novo código de 30 minutos para concluir a ativação.'];
    return ['pending','Pronto para ativar','Gere um código e peça ao cliente para preencher o login do aplicativo.'];
}
function tb_station_actions(array $r): array {
    $actions=[];
    if (($r['status'] ?? '') === 'ACTIVE') {
        if (!empty($r['eligibleDelivery'])) {
            if (!empty($r['activationConsumed']) || ($r['enrollmentState'] ?? '') === 'BOUND') {
                $actions=['reinstall','new-device','transfer'];
                if (preg_match('/^[a-f0-9]{64}$/',(string)($r['sessionId'] ?? ''))) $actions[]='revoke-session';
            } elseif (($r['enrollmentState'] ?? '') === 'PENDING_ENROLLMENT') {
                $actions[]='issue-code';
                if (!empty($r['codeIssued'])) $actions[]='cancel-code';
            }
        }
        $actions[]='block';
    } elseif (($r['status'] ?? '') === 'SUSPENDED' && !empty($r['eligibleDelivery'])) $actions[]='unblock';
    return $actions;
}
function tb_station_action_info(): array {
    return [
        'issue-code'=>['Gerar código de acesso','O novo código vale 30 minutos e só pode ser usado uma vez. O código anterior deixa de funcionar imediatamente.','Novo código de ativação solicitado pelo cliente.'],
        'reinstall'=>['Cliente reinstalou o aplicativo','Libera o vínculo da instalação anterior, encerra suas sessões e gera um código de 30 minutos para ativar novamente. Jogos e saves não são apagados por esta ação.','Cliente reinstalou o aplicativo no mesmo aparelho.'],
        'new-device'=>['Trocar de celular','O aparelho anterior perde o acesso. Um código de 30 minutos será gerado para ativar o novo celular. Continua permitido um aparelho por licença.','Cliente solicitou a troca do aparelho autorizado.'],
        'transfer'=>['Liberar outra ativação','Remove a autorização do aparelho anterior e encerra suas sessões. A licença fica pronta para receber um novo código. Esta ação não gera o código.','Liberação de novo vínculo de aparelho solicitada pelo cliente.'],
        'block'=>['Bloquear acesso','Impede novas sessões e encerra as existentes. Use em caso de aparelho perdido, roubo ou atendimento que exija suspender o acesso.','Bloqueio de acesso solicitado pelo responsável.'],
        'unblock'=>['Desbloquear acesso','Restaura o acesso de uma licença com pagamento confirmado. O vínculo do aparelho é preservado; se o aplicativo foi reinstalado, libere uma nova ativação depois.','Desbloqueio de acesso autorizado pelo responsável.'],
        'cancel-code'=>['Cancelar código emitido','O código atual deixa de funcionar. Nenhum novo código é gerado. A licença fica aguardando uma nova emissão.','Cancelamento do código de ativação solicitado pelo responsável.'],
        'revoke-session'=>['Reconectar aplicativo','Encerra a sessão atual. Na próxima comunicação o aplicativo precisará renovar a sessão com a licença e a chave salvas. O vínculo do aparelho é preservado.','Reconexão do aplicativo solicitada durante atendimento.'],
    ];
}
function tb_station_failure(string $code): string {
    return match($code) {
        'STATION_STATE_CHANGED'=>'O cadastro mudou durante o atendimento. Atualize os dados e confira o estado antes de tentar novamente.',
        'STATION_REQUEST_ALREADY_COMPLETED'=>'Essa solicitação já foi concluída. Não repetimos a alteração. Atualize o cadastro; para emitir outro código, faça uma nova confirmação.',
        'STATION_ALREADY_ACTIVATED'=>'O aparelho já está ativado. Use “Cliente reinstalou o aplicativo” ou “Trocar de celular” quando precisar liberar outra ativação.',
        'STATION_DELIVERY_NOT_ELIGIBLE','STATION_FINANCIAL_BLOCK'=>'O pagamento ou a liberação comercial ainda não está confirmado. Confira a venda antes de liberar o acesso.',
        'STATION_NOT_FOUND'=>'Licença não encontrada. Atualize a lista.',
        'STATION_RATE_LIMITED'=>'Muitas tentativas neste atendimento. Aguarde alguns minutos.',
        'STATION_RECOVERY_CODE_PENDING'=>'O aparelho anterior já foi liberado, mas o código ainda não foi entregue. Atualize o cadastro e use “Gerar código de acesso” para concluir.',
        default=>'A operação não pôde ser confirmada. Atualize o cadastro e confira o histórico antes de tentar de novo.',
    };
}
