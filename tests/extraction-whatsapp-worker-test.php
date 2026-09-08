<?php
declare(strict_types=1);
define('TURBORAMA_EXTRACTION_WORKER_TEST',true);
require __DIR__.'/../ops/production/turborama-suite-extraction-whatsapp.php';
function check(bool $value,string $message):void {if(!$value)throw new RuntimeException($message);}
foreach(['success','missing','lookup-error','begin-error','begin-lost','queue-false','queue-throws','ack-lost'] as $mode) {
    $queued=0;$completion=null;$calls=[];
    $event=str_repeat('a',64);$lease='11111111-2222-3333-4444-555555555555';
    $call=static function(string $path,array $body)use($mode,$event,$lease,&$completion,&$calls):array {
        $calls[]=$path;
        if(str_ends_with($path,'/lease'))return [200,['eventId'=>$event,'leaseToken'=>$lease,'sourcePurchaseId'=>'fixture-purchase']];
        if(str_ends_with($path,'/begin-dispatch')) {
            if($mode==='begin-lost')throw new RuntimeException('simulated timeout');
            return $mode==='begin-error'?[503,null]:[200,['eventId'=>$event,'message'=>'MENSAGEM DE TESTE']];
        }
        $completion=$body;
        check($body['eventId']===$event&&$body['leaseToken']===$lease,'Lease binding lost.');
        return [$mode==='ack-lost'?503:200,[]];
    };
    $lookup=static function(string $purchase)use($mode):?array {
        check($purchase==='fixture-purchase','Wrong purchase source.');
        if($mode==='lookup-error')throw new RuntimeException('simulated database failure');
        return $mode==='missing'?null:['id'=>42,'name'=>'Cliente Teste','phone'=>'FAKE-NO-NETWORK'];
    };
    $queue=static function(int $id,string $type,string $phone,string $message)use($mode,&$queued):bool {
        $queued++;
        check($id===42&&$type==='suite_extraction_completed'&&$phone==='FAKE-NO-NETWORK'
            &&$message==='MENSAGEM DE TESTE','Existing queue contract changed.');
        if($mode==='queue-throws')throw new RuntimeException('ambiguous queue failure');
        return $mode!=='queue-false';
    };
    $result=extraction_worker($call,$lookup,$queue);
    $expected=match($mode){'missing'=>'SKIPPED','lookup-error'=>'RETRY','begin-error','begin-lost','queue-false','queue-throws'=>'UNCERTAIN',default=>'QUEUED'};
    check(($completion['outcome']??null)===$expected,'Incorrect retry/dispatch state: '.$mode);
    check($queued===(in_array($mode,['success','queue-false','queue-throws','ack-lost'],true)?1:0),'Unexpected queue invocation.');
    check($result===($mode==='ack-lost'?2:0),'Unexpected worker result.');
    if($mode!=='lookup-error')check(($completion['outcome']??null)!=='RETRY','Unsafe automatic retry after possible dispatch.');
}
$nothing=static fn(string $path,array $body):array=>[204,null];
$unexpected=static function():never{throw new RuntimeException('No job must do no work.');};
check(extraction_worker($nothing,$unexpected,$unexpected)===0,'Empty queue.');
echo "PASS: existing queue contract, owner lookup, lease binding, no raw API send, and conservative uncertain outcomes.\n";
