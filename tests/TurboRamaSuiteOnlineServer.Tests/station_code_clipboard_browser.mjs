// Exercise actual browser clipboard contents, including a denied modern API.
import fs from 'node:fs';
import http from 'node:http';
import os from 'node:os';
import path from 'node:path';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../../',import.meta.url));
const source=process.argv[2] || path.join(root,'ops/station-admin/site/station-admin.js');
const script=fs.readFileSync(source,'utf8');
const folder=fs.mkdtempSync(path.join(os.tmpdir(),'station-clipboard-'));fs.chmodSync(folder,0o700);
const pause=ms=>new Promise(r=>setTimeout(r,ms));
const ids=new Set([...script.matchAll(/byId\('([^']+)'\)/g)].map(m=>m[1]));
const dialogs=['station-support','station-confirm','station-issued','station-registration'];
const forms=['station-action-form','station-registration-form'];
const fields=[...new Set([...script.matchAll(/\.elements\.([a-z_]+)/g)].map(m=>m[1]))];
const html='<!doctype html><meta charset="utf-8"><main data-action-info="{}">'+forms.map(id=>'<form id="'+id+'">'+fields.map(name=>'<input name="'+name+'">').join('')+'</form>').join('')+dialogs.map(id=>'<dialog id="'+id+'"></dialog>').join('')+[...ids].filter(id=>!dialogs.includes(id)&&!forms.includes(id)).map(id=>'<div id="'+id+'"></div>').join('')+'</main><script src="/station-admin.js"></script>';
const server=http.createServer((req,res)=>{res.setHeader('Content-Type',req.url==='/station-admin.js'?'text/javascript; charset=utf-8':'text/html; charset=utf-8');res.end(req.url==='/station-admin.js'?script:html);});
await new Promise(r=>server.listen(0,'127.0.0.1',r));
const origin='http://127.0.0.1:'+server.address().port;
const profile=folder+'/chrome';
const chrome=spawn('google-chrome',['--headless=new','--no-sandbox','--disable-dev-shm-usage','--disable-gpu','--remote-debugging-address=127.0.0.1','--remote-debugging-port=0','--user-data-dir='+profile,'about:blank'],{stdio:'ignore'});
let socket;
try {
 const active=profile+'/DevToolsActivePort';
 for(let i=0;i<100&&!fs.existsSync(active);i++)await pause(100);
 const port=fs.readFileSync(active,'utf8').split('\n')[0];
 const targets=await(await fetch('http://127.0.0.1:'+port+'/json/list')).json();
 socket=new WebSocket(targets.find(t=>t.type==='page').webSocketDebuggerUrl);
 await new Promise((r,j)=>{socket.onopen=r;socket.onerror=j;});
 let next=0;const pending=new Map(),errors=[];
 socket.onmessage=e=>{const x=JSON.parse(e.data);if(x.id){const p=pending.get(x.id);pending.delete(x.id);x.error?p.reject(new Error('CDP failed')):p.resolve(x.result);}else if(x.method==='Runtime.exceptionThrown')errors.push(x.params.exceptionDetails.text);};
 const send=(method,params={})=>new Promise((resolve,reject)=>{const id=++next;pending.set(id,{resolve,reject});socket.send(JSON.stringify({id,method,params}));});
 const evaluate=async expression=>{const x=await send('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true,userGesture:true});if(x.exceptionDetails)throw new Error('Clipboard fixture failed');return x.result.value;};
 await send('Page.enable');await send('Runtime.enable');
 await send('Browser.grantPermissions',{origin,permissions:['clipboardReadWrite','clipboardSanitizedWrite']});
 await send('Page.navigate',{url:origin});
 for(let i=0;i<100;i++){if(await evaluate("document.readyState==='complete' && !!document.getElementById('station-copy-code')"))break;await pause(100);}
 await send('Page.bringToFront');
 const result=await evaluate(`(async()=>{
   const dialog=document.getElementById('station-issued'), node=document.getElementById('station-code'), button=document.getElementById('station-copy-code');
   dialog.append(node,button);dialog.showModal();
   const clipboard=navigator.clipboard, nativeWrite=clipboard.writeText.bind(clipboard), nativeRead=clipboard.readText.bind(clipboard), nativeCommand=document.execCommand.bind(document);
   const old='previous synthetic code', current='AbCdEf_-0123456789'.repeat(3).slice(0,42)+'A';
   const cases=[];
   for(const mode of ['modern','denied','unavailable','both-denied','empty']) {
     await nativeWrite(old);node.textContent=mode==='empty'?'':current;
     button.textContent='Copiar código';
     if(mode==='unavailable')Object.defineProperty(navigator,'clipboard',{configurable:true,value:undefined});
     else clipboard.writeText=mode==='modern'||mode==='empty'?nativeWrite:()=>Promise.reject(new DOMException('Synthetic permission rejection','NotAllowedError'));
     document.execCommand=mode==='both-denied'?()=>false:nativeCommand;
     button.click();await new Promise(r=>setTimeout(r,100));
     const actual=await nativeRead(), expectedCopied=['modern','denied','unavailable'].includes(mode);
     cases.push({mode,exactCodeCopied:actual===current,previousClipboardRetained:actual===old,label:button.textContent,noTemporarySecretField:!dialog.querySelector('textarea'),buttonEnabled:!button.disabled,
       passed:(expectedCopied?actual===current&&button.textContent==='Código copiado':actual===old&&button.textContent!=='Código copiado')&&!dialog.querySelector('textarea')&&!button.disabled});
     if(mode==='unavailable')delete navigator.clipboard;
     clipboard.writeText=nativeWrite;document.execCommand=nativeCommand;
   }
   dialog.close();
   return {cases,closedDialogClearsCode:node.textContent==='',browserStorageEmpty:localStorage.length===0&&sessionStorage.length===0};
 })()`);
 result.browserExceptions=errors.length;
 if(!result.cases.every(c=>c.passed)||!result.closedDialogClearsCode||!result.browserStorageEmpty||errors.length)throw new Error('Clipboard behavior regression: '+JSON.stringify(result));
 console.log(JSON.stringify(result));
 await send('Browser.close').catch(()=>{});
} finally {
 socket?.close();chrome.kill('SIGTERM');await new Promise(r=>server.close(r));
 await pause(250);fs.rmSync(folder,{recursive:true,force:true});
}
