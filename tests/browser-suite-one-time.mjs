import http from 'node:http';
import fs from 'node:fs';
import {spawn} from 'node:child_process';
const html=fs.readFileSync(process.argv[2],'utf8');const javascript=fs.readFileSync(process.argv[3],'utf8');
const requests=[];const server=http.createServer((request,response)=>{requests.push({method:request.method,url:request.url});response.setHeader('Content-Security-Policy',"default-src 'none'; style-src 'self'; script-src 'self'; img-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'");response.setHeader('Cache-Control','no-store');if(request.url==='/admin/assets/admin.js'){response.setHeader('Content-Type','text/javascript');response.end(javascript);}else if(request.url==='/admin/suite/issued'){response.setHeader('Content-Type','text/html; charset=utf-8');response.end('<!doctype html><h1>OTP não exibido</h1>');}else{response.setHeader('Content-Type','text/html; charset=utf-8');response.end(html);}});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));const port=server.address().port;const origin=`http://127.0.0.1:${port}`;
const chrome=spawn('/usr/bin/google-chrome',['--headless=new','--no-sandbox','--disable-gpu','--remote-debugging-pipe',`--user-data-dir=/tmp/chrome-suite-${process.pid}`,'about:blank'],{stdio:['ignore','ignore','pipe','pipe','pipe']});
let serial=0,buffer=Buffer.alloc(0);const pending=new Map(),events=[];chrome.stderr.on('data',data=>events.push(data.toString()));chrome.stdio[4].on('data',data=>{buffer=Buffer.concat([buffer,data]);for(;;){const end=buffer.indexOf(0);if(end<0)break;const raw=buffer.subarray(0,end).toString();buffer=buffer.subarray(end+1);if(!raw)continue;const message=JSON.parse(raw);if(message.id&&pending.has(message.id)){const {resolve,reject}=pending.get(message.id);pending.delete(message.id);message.error?reject(new Error(JSON.stringify(message.error))):resolve(message.result);}}});
const send=(method,params={},sessionId)=>new Promise((resolve,reject)=>{const id=++serial;pending.set(id,{resolve,reject});chrome.stdio[3].write(JSON.stringify({id,method,params,...(sessionId?{sessionId}:{})})+'\0');});
try{
 const {targetId}=await send('Target.createTarget',{url:'about:blank'});const attached=await send('Target.attachToTarget',{targetId,flatten:true});const session=attached.sessionId;
 await send('Page.enable',{},session);await send('Browser.grantPermissions',{permissions:['clipboardReadWrite'],origin});await send('Runtime.enable',{},session);await send('Log.enable',{},session);
 await send('Page.navigate',{url:origin+'/admin/suite/post'},session);await new Promise(resolve=>setTimeout(resolve,1200));
 const state=await send('Runtime.evaluate',{expression:"({path:location.pathname,inline:!!document.querySelector('[onclick],script:not([src])'),button:document.querySelector('[data-copy-target]')?.textContent})",returnByValue:true},session);
 await send('Runtime.evaluate',{expression:"document.querySelector('[data-copy-target]').click()"},session);await new Promise(resolve=>setTimeout(resolve,300));
 const after=await send('Runtime.evaluate',{expression:"({path:location.pathname,button:document.querySelector('[data-copy-target]')?.textContent})",returnByValue:true},session);
 const first=state.result.value,second=after.result.value;
 await send('Page.reload',{},session);await new Promise(resolve=>setTimeout(resolve,700));
 const refreshed=(await send('Runtime.evaluate',{expression:"({path:location.pathname,secret:!!document.querySelector('#activation-code'),button:!!document.querySelector('[data-copy-target]')})",returnByValue:true},session)).result.value;
 const errors=events.join('').split('\n').filter(line=>/Content Security Policy|Refused to|SEVERE/i.test(line));const posts=requests.filter(r=>r.method==='POST');
 if(first.inline||first.button!=='Copiar código'||second.path!=='/admin/suite/issued'||second.button!=='Copiado'||refreshed.path!=='/admin/suite/issued'||refreshed.secret||refreshed.button||posts.length||errors.length)throw new Error(JSON.stringify({first,second,refreshed,posts,errors}));
 console.log('BROWSER SUITE TEST: OK (CSP, copiar, atualização real sem POST e segredo não reaparece)');
}finally{chrome.kill('SIGTERM');server.close();}
