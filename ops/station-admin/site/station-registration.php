<?php
declare(strict_types=1);

function tb_station_registration_schema(PDO $db): void {
    $db->exec("CREATE TABLE IF NOT EXISTS station_registrations (
        request_id TEXT PRIMARY KEY,
        actor_id INTEGER NOT NULL REFERENCES users(id),
        customer_user_id INTEGER NOT NULL REFERENCES users(id),
        payload_digest TEXT NOT NULL,
        display_name TEXT NOT NULL,
        grant_kind TEXT NOT NULL CHECK(grant_kind IN ('paid','courtesy','test')),
        reason TEXT NOT NULL,
        allow_additional INTEGER NOT NULL DEFAULT 0,
        state TEXT NOT NULL DEFAULT 'PREPARED' CHECK(state IN ('PREPARED','CREATED','COMPLETED')),
        license_id TEXT,
        created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
        updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
    )");
}

function tb_station_register_customer(PDO $db,array $admin): never {
    $req=(string)($_POST['request_id'] ?? '');
    $mode=(string)($_POST['customer_mode'] ?? '');
    $kind=(string)($_POST['grant_kind'] ?? '');
    $reason=trim((string)($_POST['reason'] ?? ''));
    $name=trim((string)($_POST['customer_name'] ?? ''));
    $email=strtolower(trim((string)($_POST['customer_email'] ?? '')));
    $phone=trim((string)($_POST['customer_phone'] ?? ''));
    $additional=($_POST['allow_additional'] ?? '')==='1';
    $customerId=filter_var($_POST['customer_id'] ?? '',FILTER_VALIDATE_INT,['options'=>['min_range'=>1]]);
    if(!preg_match('/^[a-f0-9]{32}$/',$req)||!in_array($mode,['new','existing'],true)||
       !in_array($kind,['paid','courtesy','test'],true)||($_POST['confirm_grant'] ?? '')!=='1'||
       mb_strlen($reason)<10||mb_strlen($reason)>200||preg_match('/[\x00-\x1f\x7f]/u',$reason))
        station_json(400,['ok'=>false,'message'=>'Escolha o cliente, o tipo de liberação e confirme a autorização. Informe o motivo de 10 a 200 caracteres.']);
    if($mode==='new') {
        if(mb_strlen($name)<2||mb_strlen($name)>120||preg_match('/[\x00-\x1f\x7f]/u',$name)||
           strlen($email)>254||!filter_var($email,FILTER_VALIDATE_EMAIL)||strlen($phone)>32||
           ($phone!==''&&!tb_phone_e164($phone))||$additional)
            station_json(400,['ok'=>false,'message'=>'Informe nome e e-mail válidos. O WhatsApp é opcional; se informado, inclua o DDD.']);
        $phone=$phone!==''?(tb_phone_e164($phone) ?? ''):'';$customerId=0;
    } else {
        if($customerId===false)station_json(400,['ok'=>false,'message'=>'Escolha um cliente cadastrado na lista.']);
        $name='';$email='';$phone='';
    }
    if(!tb_rate_limit('station_register_admin_'.(int)$admin['id'],20,900))
        station_json(429,['ok'=>false,'message'=>'Muitas tentativas. Aguarde alguns minutos.']);
    $verify=$db->prepare("SELECT password_hash FROM users WHERE id=? AND role='admin' AND status='active'");
    $verify->execute([(int)$admin['id']]);
    if(!password_verify((string)($_POST['password'] ?? ''),(string)$verify->fetchColumn()))
        station_json(403,['ok'=>false,'message'=>'Senha administrativa incorreta. Nenhum cadastro ou licença foi criado.']);
    // The receipt contains no password or activation code. Retries keep the same customer/license.
    $digest=hash('sha256',json_encode([$mode,$customerId,$name,$email,$phone,$kind,$reason,$additional],JSON_THROW_ON_ERROR));
    $license='';$prepared=false;
    try {
        tb_station_registration_schema($db);tb_station_schema($db);
        $db->beginTransaction();
        $lookup=$db->prepare('SELECT * FROM station_registrations WHERE request_id=?');$lookup->execute([$req]);$registration=$lookup->fetch();
        if($registration) {
            if((int)$registration['actor_id']!==(int)$admin['id']||!hash_equals($registration['payload_digest'],$digest)) {
                $db->rollBack();station_json(409,['ok'=>false,'message'=>'Os dados desta solicitação mudaram. Feche o formulário e abra um novo cadastro.']);
            }
            $customerId=(int)$registration['customer_user_id'];
        } else {
            if($mode==='new') {
                $lookup=$db->prepare('SELECT id FROM users WHERE lower(email)=?');$lookup->execute([$email]);
                if($lookup->fetchColumn()) {
                    $db->rollBack();station_json(409,['ok'=>false,'message'=>'Este e-mail já está cadastrado. Escolha “Cliente já cadastrado” para liberar o Station.']);
                }
                // App enrollment does not use a portal password. No notification is queued here.
                $db->prepare("INSERT INTO users(name,email,phone,password_hash,role,status) VALUES(?,?,?,?,'customer','active')")
                   ->execute([$name,$email,$phone,password_hash(bin2hex(random_bytes(32)),PASSWORD_DEFAULT)]);
                $customerId=(int)$db->lastInsertId();
            }
            $lookup=$db->prepare("SELECT id,name FROM users WHERE id=? AND role='customer' AND status='active'");
            $lookup->execute([$customerId]);$customer=$lookup->fetch();
            if(!$customer) {$db->rollBack();station_json(400,['ok'=>false,'message'=>'Cliente não disponível. Selecione um cliente ativo ou cadastre um novo.']);}
            $name=(string)$customer['name'];
            $db->prepare('INSERT INTO station_registrations(request_id,actor_id,customer_user_id,payload_digest,display_name,grant_kind,reason,allow_additional) VALUES(?,?,?,?,?,?,?,?)')
               ->execute([$req,(int)$admin['id'],$customerId,$digest,$name,$kind,$reason,$additional?1:0]);
            $registration=['display_name'=>$name,'state'=>'PREPARED','license_id'=>null];
        }
        $lookup=$db->prepare("SELECT id,phone FROM users WHERE id=? AND role='customer' AND status='active'");
        $lookup->execute([$customerId]);$customer=$lookup->fetch();
        if(!$customer) {$db->rollBack();station_json(400,['ok'=>false,'message'=>'Este cliente está arquivado ou indisponível. Confira o cadastro antes de liberar o acesso.']);}
        $db->commit();$prepared=true;
        $license=(string)($registration['license_id'] ?? '');
        if($registration['state']==='COMPLETED')
            station_json(409,['ok'=>false,'partial'=>true,'licenseId'=>$license,'message'=>'O cadastro e a emissão já foram concluídos. O código não é exibido novamente. Abra o atendimento para emitir outro, se precisar.']);
        $controls=['actor'=>'turbobox-admin-'.(int)$admin['id'],'requestId'=>$req,'reason'=>$reason,
                   'stepUpAt'=>time(),'clientIpDigest'=>hash('sha256','station-admin|'.tb_client_ip()),'csrfVerified'=>true];
        if($license==='') {
            [$status,$result]=tb_station_admin_request('POST','/management/registrations',$controls+[
                'customerRef'=>'TBX-USER-'.$customerId,'displayName'=>(string)$registration['display_name'],
                'grantKind'=>$kind,'allowAdditional'=>$additional]);
            if($status!==200) {
                $code=(string)($result['code'] ?? '');
                if($code==='STATION_CUSTOMER_ALREADY_LICENSED')
                    station_json(409,['ok'=>false,'licenseId'=>(string)($result['licenseId'] ?? ''),
                        'message'=>'Este cliente já tem uma licença Station. Abra o atendimento para gerar código ou trocar o celular. Para dois aparelhos simultâneos, confirme “Liberar mais um aparelho” em um novo cadastro.']);
                station_json($status,['ok'=>false,'partial'=>true,'retrySame'=>true,'message'=>
                    $code==='STATION_RATE_LIMITED'?tb_station_failure($code):'O cliente foi salvo, mas a liberação não pôde ser confirmada. Confira os dados e tente concluir este mesmo formulário.']);
            }
            $license=(string)($result['licenseId'] ?? '');
            if(!preg_match('/^STA-[A-Z0-9_-]{6,64}$/',$license))throw new RuntimeException();
            $db->prepare("UPDATE station_registrations SET license_id=?,state='CREATED',updated_at=CURRENT_TIMESTAMP WHERE request_id=?")
               ->execute([$license,$req]);
        }
        $current=station_support($license)['license'];
        if(!in_array('issue-code',$current['allowedActions'],true))
            station_json(409,['ok'=>false,'partial'=>true,'licenseId'=>$license,'message'=>'A licença foi cadastrada, mas mudou de situação. Abra o atendimento para conferir a ativação e as ações disponíveis.']);
        $controls['requestId']=substr(hash('sha256',$req.'|code'),0,32);
        $controls['expectedGeneration']=(int)$current['revocationGeneration'];
        $controls['expectedActivationGeneration']=(int)$current['activationGeneration'];
        [$status,$result]=tb_station_admin_request('POST','/management/licenses/'.rawurlencode($license).'/issue-code',$controls);
        if($status!==200) {
            $completed=($result['code'] ?? '')==='STATION_REQUEST_ALREADY_COMPLETED';
            if($completed)$db->prepare("UPDATE station_registrations SET state='COMPLETED',updated_at=CURRENT_TIMESTAMP WHERE request_id=?")->execute([$req]);
            station_json($status,['ok'=>false,'partial'=>true,'licenseId'=>$license,'retrySame'=>false,
                'message'=>$completed?'A licença e o código já foram criados. O código não pode ser recuperado. Abra o atendimento e confirme uma nova emissão.':'Cliente e licença cadastrados. A emissão não pôde ser confirmada; abra o atendimento e confira o histórico antes de gerar outro código.']);
        }
        $code=(string)($result['activationCode'] ?? '');
        if(!preg_match('/^[A-Za-z0-9_-]{43}$/',$code))throw new RuntimeException();
        // Once issued, secondary bookkeeping failures must not hide the only copy of the code.
        try {
            $db->prepare("UPDATE station_registrations SET state='COMPLETED',updated_at=CURRENT_TIMESTAMP WHERE request_id=?")->execute([$req]);
            tb_station_save_contact($db,$license,(string)$customer['phone'],$customerId,(string)$registration['display_name'],'registration');
            tb_audit($db,(int)$admin['id'],'station_customer_registered','station_license',null,$license.'; '.$kind);
        } catch(Throwable) {}
        station_json(200,['ok'=>true,'action'=>'register-customer','licenseId'=>$license,
            'message'=>'Cliente cadastrado e acesso Station liberado. Copie o código para ativar o aplicativo.',
            'issued'=>['code'=>$code,'expires'=>tb_station_format_expiry((string)$result['expiresAt']),
                'expiresAt'=>(string)$result['expiresAt'],'licenseId'=>$license,
                'delivery'=>'Nenhuma mensagem foi enviada. Copie o código e entregue ao cliente por canal privado.']]);
    } catch(Throwable) {
        if($db->inTransaction())$db->rollBack();
        station_json(503,['ok'=>false,'partial'=>$prepared,'licenseId'=>$license,'retrySame'=>$license==='',
            'message'=>$license!==''?'Cliente e licença cadastrados. A resposta do código não pôde ser confirmada. Abra o atendimento e confira o histórico antes de emitir outro.':($prepared?'O cliente foi salvo. A liberação precisa ser conferida; tente concluir este mesmo formulário.':'Não foi possível salvar o cadastro. Nenhuma licença foi solicitada; tente novamente.')]);
    }
}
