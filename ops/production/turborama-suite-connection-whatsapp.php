<?php
declare(strict_types=1);
require_once '/home/lz-servidor/HOSTINGER SITE DOCUMENTOS/sistema2026.lzgames.com.br/public_html/turbobox/notification-lib.php';
$socket=getenv('TURBORAMA_SUITE_ADMIN_SOCKET')?:getenv('SUITE_ADMIN_SOCKET')?:'/run/turborama-suite-admin/admin.sock';
$tokenFile=getenv('TURBORAMA_SUITE_ADMIN_TOKEN_FILE')?:getenv('SUITE_ADMIN_TOKEN_FILE')?:'';
$token=is_file($tokenFile)?trim((string)file_get_contents($tokenFile)):'';
if($tokenFile===''){fwrite(STDERR,"SUITE CONNECTION NOTICE: variável de credencial indisponível\n");exit(2);}
if(!is_readable($tokenFile)){fwrite(STDERR,"SUITE CONNECTION NOTICE: arquivo de credencial indisponível\n");exit(2);}
if($token===''){fwrite(STDERR,"SUITE CONNECTION NOTICE: credencial vazia\n");exit(2);}
if(!file_exists($socket)){fwrite(STDERR,"SUITE CONNECTION NOTICE: socket indisponível\n");exit(2);}
function suite_call(string $socket,string $token,string $path,?array $body=null):array{
 $args=['/usr/bin/curl','--silent','--show-error','--unix-socket',$socket,'-H','X-Suite-Admin-Token: '.$token,'-H','Content-Type: application/json','-X','POST','http://localhost/'.$path,'-w',"\n%{http_code}"];
 if($body!==null){$args[]='--data-binary';$args[]=json_encode($body,JSON_THROW_ON_ERROR|JSON_UNESCAPED_SLASHES);}
 $spec=[1=>['pipe','w'],2=>['pipe','w']];$proc=proc_open($args,$spec,$pipes);if(!is_resource($proc))throw new RuntimeException('bridge unavailable');
 $raw=stream_get_contents($pipes[1]);$err=stream_get_contents($pipes[2]);fclose($pipes[1]);fclose($pipes[2]);$exit=proc_close($proc);
 if($exit!==0)throw new RuntimeException('bridge failed');$cut=strrpos($raw,"\n");return [(int)substr($raw,$cut+1),substr($raw,0,$cut)];
}
[$status,$raw]=suite_call($socket,$token,'connection-notifications/lease');if($status===204)exit(0);if($status!==200)exit(2);
$job=json_decode($raw,true,16,JSON_THROW_ON_ERROR);$outcome='RETRY';$error='BRIDGE_ERROR';
try{
 $db=tb_db();$q=$db->prepare('SELECT u.id,u.name,u.phone FROM purchases p JOIN users u ON u.id=p.user_id WHERE p.id=? AND p.status=\'paid\' AND u.status=\'active\'');$q->execute([(int)$job['sourcePurchaseId']]);$customer=$q->fetch();
 if(!$customer||!tb_phone_e164((string)$customer['phone'])){$outcome='SKIPPED';$error='RECIPIENT_UNAVAILABLE';}
 else{$first=trim(explode(' ',trim((string)$customer['name']))[0]??'cliente');$at=new DateTimeImmutable((string)$job['connectedAt']);$at=$at->setTimezone(new DateTimeZone('America/Maceio'));$message="Olá, {$first}. Seu computador foi conectado com sucesso à sua licença LZ Games em ".$at->format('d/m/Y H:i').". Se não foi você, fale com nosso suporte imediatamente.";if(!tb_queue_whatsapp((int)$customer['id'],'suite_device_connected',(string)$customer['phone'],$message))throw new RuntimeException('queue refused');$outcome='SENT';$error='';}
}catch(Throwable){$outcome=((int)($job['attempts']??1)>=8)?'DEAD':'RETRY';$error='DELIVERY_FAILED';}
[$done]=suite_call($socket,$token,'connection-notifications/complete',['eventId'=>$job['eventId'],'outcome'=>$outcome,'errorCode'=>$error]);exit($done===200?0:2);
