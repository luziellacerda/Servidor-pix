<?php
declare(strict_types=1);

// Reuses the same supported queue and server-side customer lookup as the LOGIN
// worker. No direct MenuIA call, invented recipient or client-supplied phone.
function extraction_customer_for_purchase(PDO $db, string $purchase): ?array {
    if ($purchase === '' || strlen($purchase) > 64) return null;
    $query = $db->prepare("SELECT u.id,u.name,u.phone FROM payment_orders o JOIN purchases p ON p.id=o.purchase_id JOIN users u ON u.id=p.user_id WHERE o.public_id=? AND o.status='paid' AND p.status='paid' AND u.status='active' LIMIT 2");
    $query->execute([$purchase]);
    $rows = $query->fetchAll(PDO::FETCH_ASSOC);
    if (count($rows) !== 1) return null;
    $phone = tb_phone_e164((string)$rows[0]['phone']);
    if (!$phone || (function_exists('tb_whatsapp_recipient_blocked') && tb_whatsapp_recipient_blocked($phone))) return null;
    // Preserve the existing recipient policy before crossing the dispatch boundary.
    return ['id' => (int)$rows[0]['id'], 'name' => (string)$rows[0]['name'], 'phone' => $phone];
}

function extraction_worker(callable $call, callable $customerForPurchase, callable $queue): int {
    [$status,$job]=$call('extraction-notifications/lease',[]);
    if($status===204)return 0;
    if($status!==200||!is_array($job))return 2;
    $event=(string)($job['eventId']??'');$lease=(string)($job['leaseToken']??'');
    if(!preg_match('/^[a-f0-9]{64}$/D',$event)||!preg_match('/^[a-f0-9-]{36}$/D',$lease))return 2;
    $outcome='RETRY';$error='RECIPIENT_LOOKUP_FAILED';$dispatchStarted=false;
    try {
        $customer=$customerForPurchase((string)($job['sourcePurchaseId']??''));
        if($customer===null){$outcome='SKIPPED';$error='RECIPIENT_UNAVAILABLE';}
        else {
            // Once begin-dispatch is attempted its response could be lost after
            // commit. Such jobs are not automatically replayed into WhatsApp.
            $dispatchStarted=true;
            [$begin,$render]=$call('extraction-notifications/begin-dispatch',[
                'eventId'=>$event,'leaseToken'=>$lease,'customerName'=>(string)$customer['name']]);
            if($begin!==200||!is_array($render)||($render['eventId']??'')!==$event
                ||!is_string($render['message']??null)||strlen($render['message'])>8192) {
                $outcome='UNCERTAIN';$error='DISPATCH_NOT_CONFIRMED';
            } else {
                $accepted=$queue((int)$customer['id'],'suite_extraction_completed',
                    (string)$customer['phone'],$render['message']);
                $outcome=$accepted?'QUEUED':'UNCERTAIN';
                $error=$accepted?'':'QUEUE_OUTCOME_UNKNOWN';
            }
        }
    } catch(Throwable) {
        $outcome=$dispatchStarted?'UNCERTAIN':'RETRY';
        $error=$dispatchStarted?'QUEUE_OUTCOME_UNKNOWN':'RECIPIENT_LOOKUP_FAILED';
    }
    [$done]=$call('extraction-notifications/complete',[
        'eventId'=>$event,'leaseToken'=>$lease,'outcome'=>$outcome,'errorCode'=>$error]);
    return $done===200?0:2;
}

// Unit tests include this file without loading production code or credentials.
if(defined('TURBORAMA_EXTRACTION_WORKER_TEST'))return;
try {
    if(!function_exists('curl_init'))throw new RuntimeException('CURL_UNAVAILABLE');
    $library=getenv('TURBORAMA_NOTIFICATION_LIBRARY')?:'/home/lz-servidor/HOSTINGER SITE DOCUMENTOS/sistema2026.lzgames.com.br/public_html/turbobox/notification-lib.php';
    if(!is_readable($library))throw new RuntimeException('LIBRARY_UNAVAILABLE');
    require_once $library;
    $socket=getenv('TURBORAMA_SUITE_ADMIN_SOCKET')?:getenv('SUITE_ADMIN_SOCKET')?:'/run/turborama-suite-admin/admin.sock';
    $tokenFile=getenv('TURBORAMA_SUITE_ADMIN_TOKEN_FILE')?:getenv('SUITE_ADMIN_TOKEN_FILE')?:'';
    if(!is_readable($tokenFile)||filesize($tokenFile)>8192||!file_exists($socket))throw new RuntimeException('BRIDGE_UNAVAILABLE');
    $token=trim((string)file_get_contents($tokenFile));
    if($token===''||str_contains($token,"\n")||str_contains($token,"\r"))throw new RuntimeException('TOKEN_UNAVAILABLE');
    $call=static function(string $path,array $body)use($socket,$token):array {
        $curl=curl_init('http://localhost/'.$path);
        curl_setopt_array($curl,[CURLOPT_UNIX_SOCKET_PATH=>$socket,CURLOPT_PROXY=>'',
            CURLOPT_POST=>true,CURLOPT_RETURNTRANSFER=>true,CURLOPT_FOLLOWLOCATION=>false,
            CURLOPT_CONNECTTIMEOUT=>3,CURLOPT_TIMEOUT=>10,
            CURLOPT_HTTPHEADER=>['Content-Type: application/json','X-Suite-Admin-Token: '.$token],
            CURLOPT_POSTFIELDS=>json_encode($body,JSON_THROW_ON_ERROR)]);
        $raw=curl_exec($curl);$status=(int)curl_getinfo($curl,CURLINFO_HTTP_CODE);curl_close($curl);
        if($raw===false||strlen($raw)>16384)throw new RuntimeException('BRIDGE_FAILED');
        return [$status,$raw===''?null:json_decode($raw,true,8,JSON_THROW_ON_ERROR)];
    };
    $lookup=static function(string $purchase):?array {
        return extraction_customer_for_purchase(tb_db(), $purchase);
    };
    exit(extraction_worker($call,$lookup,static fn(int $id,string $type,string $phone,string $message):bool =>
        (bool)tb_queue_whatsapp($id,$type,$phone,$message)));
}catch(Throwable){fwrite(STDERR,"SUITE EXTRACTION NOTICE: execução indisponível; conferir estado da fila\n");exit(2);}
