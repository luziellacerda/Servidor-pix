(() => {
  'use strict';
  const byId = id => document.getElementById(id);
  const config = JSON.parse(document.querySelector('main').dataset.actionInfo);
  const form = byId('station-action-form');
  const support = byId('station-support');
  if(matchMedia('(max-width:800px)').matches)document.querySelector('nav [aria-current="page"]')?.scrollIntoView({block:'nearest',inline:'center'});
  let selected = null, busy = false, changed = false, supportRequest = 0;
  const escaped = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const date = value => {
    if (!value) return 'Sem registro';
    const d = new Date(value);
    return Number.isNaN(d.getTime()) ? 'Sem registro' : d.toLocaleString('pt-BR', {timeZone:'America/Maceio', dateStyle:'short', timeStyle:'short'});
  };
  const notice = (message, error = false) => {
    const node = byId('station-notice'); node.textContent = message;
    node.classList.remove('station-hidden');node.classList.toggle('cleanup-error', error);
  };
  const json = async (url, options = {}) => {
    const response = await fetch(url, {credentials:'same-origin', cache:'no-store', redirect:'error', ...options});
    if (!response.headers.get('Content-Type')?.includes('application/json')) throw new Error('Sua sessão pode ter expirado. Atualize a página e entre novamente.');
    return {status:response.status, data:await response.json()};
  };
  const eventLabel = event => ({STATION_CODE_ISSUED:'Código emitido',STATION_TRANSFER:'Aparelho liberado',STATION_BLOCK:'Acesso bloqueado',STATION_UNBLOCK:'Acesso desbloqueado',STATION_CANCEL_CODE:'Código cancelado',STATION_REVOKE_SESSION:'Sessão encerrada'}[event] || 'Atualização da licença');
  const showSupport = async id => {
    const request=++supportRequest;selected=null;
    byId('station-support-body').innerHTML = '<p class="station-muted" role="status">Consultando o cadastro…</p>';
    if (!support.open) support.showModal();
    try {
      const {status,data} = await json(`/admin/station?format=json&license=${encodeURIComponent(id)}`);
      if(request!==supportRequest || !support.open)return;
      if (status !== 200 || !data.license) throw new Error(data.message || 'Não foi possível consultar a licença.');
      selected = data.license;
      const r = selected, [state,label,hint] = r.presentation;
      byId('station-support-title').textContent = r.displayName || 'Cliente Station';
      byId('station-support-body').innerHTML = `<div class="station-support-status"><span class="station-badge station-${escaped(state)}">${escaped(label)}</span><p>${escaped(hint)}</p></div>
        <dl class="station-facts"><div><dt>Licença</dt><dd>${escaped(r.licenseId)}</dd></div><div><dt>Pedido</dt><dd>${escaped(r.sourcePurchaseId || 'Sem pedido vinculado')}</dd></div><div><dt>Aparelho vinculado</dt><dd>${escaped(r.deviceLinked ? `${r.manufacturer} ${r.model}`.trim() : 'Nenhum aparelho vinculado')}</dd></div><div><dt>Último contato do aplicativo</dt><dd>${escaped(date(r.lastContactAt))}</dd></div></dl>
        <h3>Resolver atendimento</h3><div class="station-action-grid">${r.allowedActions.map(action => `<button type="button" class="station-operation ${action==='block'?'station-danger':''}" data-action="${escaped(action)}"><strong>${escaped(config[action][0])}</strong><span>${escaped(config[action][1])}</span></button>`).join('') || '<p class="station-muted">Nenhuma alteração disponível. Confira o pagamento e o histórico da licença.</p>'}</div>
        <details class="station-history"><summary>Histórico de atendimento · últimos 50 registros</summary>${data.history.length ? `<ol>${data.history.map(h => `<li><strong>${escaped(eventLabel(h.event))}</strong><time>${escaped(date(h.createdAt))}</time><span>${escaped(h.actor || 'Servidor Station')}</span>${h.reason ? `<p>${escaped(h.reason)}</p>`:''}</li>`).join('')}</ol>`:'<p class="station-muted">Nenhum atendimento administrativo registrado.</p>'}</details>
        <details class="station-history"><summary>Aparelhos registrados · últimos 50</summary>${data.devices.length ? `<ul>${data.devices.map(d => `<li><strong>${escaped(`${d.manufacturer} ${d.model}`.trim() || 'Aparelho Android')}</strong><span>${d.status==='ACTIVE'?'Autorizado':'Autorização encerrada'} · ${escaped(date(d.updatedAt))}</span></li>`).join('')}</ul>`:'<p class="station-muted">Nenhum aparelho foi ativado.</p>'}</details>`;
      byId('station-support-body').querySelectorAll('[data-action]').forEach(button => button.addEventListener('click', () => chooseAction(button.dataset.action)));
    } catch (error) {if(request===supportRequest && support.open)byId('station-support-body').textContent=error.message || 'Atendimento indisponível.';}
  };
  const chooseAction = action => {
    if (!selected?.allowedActions.includes(action) || busy) return;
    form.reset();const r=selected;
    form.elements.license_id.value=r.licenseId;form.elements.action.value=action;
    form.elements.generation.value=r.revocationGeneration;
    form.elements.activation_generation.value=r.activationGeneration;
    form.elements.session_id.value=r.sessionId || '';
    form.elements.request_id.value=Array.from(crypto.getRandomValues(new Uint8Array(16)), b => b.toString(16).padStart(2,'0')).join('');
    byId('station-confirm-title').textContent=config[action][0];
    byId('station-confirm-client').textContent=`${r.displayName || 'Cliente Station'} · ${r.licenseId}`;
    byId('station-effect').textContent=config[action][1];
    byId('station-reason').value=config[action][2];
    byId('station-delivery-options').classList.toggle('station-hidden', !['issue-code','reinstall','new-device'].includes(action) || r.isTest);
    byId('station-action-error').classList.add('station-hidden');
    byId('station-confirm').showModal();
  };
  document.querySelectorAll('[data-find-client]').forEach(link => link.addEventListener('click', event => {
    event.preventDefault();
    byId('station-clients').scrollIntoView({block:'start'});
    byId('station-client-search').focus({preventScroll:true});
  }));
  document.querySelectorAll('[data-access-license]').forEach(button => button.addEventListener('click', async () => {
    if (busy) return;
    const id=button.dataset.accessLicense, action=button.dataset.accessAction;
    await showSupport(id);
    if (!support.open || selected?.licenseId!==id) return;
    if (!selected.allowedActions.includes(action)) {
      notice('A situação da licença mudou. Confira as ações disponíveis no atendimento.', true);
      return;
    }
    chooseAction(action);
  }));
  document.querySelectorAll('[data-support]').forEach(button => button.addEventListener('click', () => showSupport(button.dataset.support)));
  document.querySelectorAll('[data-close]').forEach(button => button.addEventListener('click', () => {if (!busy) byId(button.dataset.close).close();}));
  document.querySelectorAll('dialog').forEach(dialog => dialog.addEventListener('cancel', event => {if (busy) event.preventDefault();}));
  byId('station-confirm').addEventListener('close', () => {form.elements.password.value='';});
  support.addEventListener('close', () => {++supportRequest;selected=null;if(changed) location.reload();});
  const clearCode = () => {byId('station-code').textContent='';byId('station-code-expiry').textContent='';};
  byId('station-issued').addEventListener('close', () => {clearCode();if(changed)location.reload();});
  form.addEventListener('submit', async event => {
    event.preventDefault();if(busy || !form.reportValidity())return;
    const payload=new FormData(form);busy=true;
    form.querySelectorAll('button').forEach(button => button.disabled=true);
    byId('station-submit').textContent='Concluindo atendimento…';
    byId('station-action-error').classList.add('station-hidden');
    try {
      const {status,data}=await json('/admin/station?format=json',{method:'POST',body:payload});
      form.elements.password.value='';
      if (!data.ok) {
        byId('station-action-error').textContent=data.message || 'A operação não pôde ser confirmada.';
        byId('station-action-error').classList.remove('station-hidden');
        if(data.partial || status===409){changed=true;notice(data.message,true);}
        return;
      }
      changed=true;notice(data.message);
      byId('station-confirm').close();
      if(data.issued) {
        byId('station-code').textContent=data.issued.code;
        byId('station-code-expiry').textContent=`Válido até ${data.issued.expires}`;
        byId('station-code-delivery').textContent=data.issued.delivery;
        byId('station-copy-code').textContent='Copiar código';
        byId('station-issued').showModal();
      } else await showSupport(payload.get('license_id'));
    } catch(error) {
      form.elements.password.value='';
      byId('station-action-error').textContent=error.message || 'A resposta não chegou. Confira o histórico antes de repetir.';
      byId('station-action-error').classList.remove('station-hidden');
    } finally {
      busy=false;form.querySelectorAll('button').forEach(button => button.disabled=false);
      byId('station-submit').textContent='Confirmar ação';
    }
  });
  byId('station-copy-code').addEventListener('click', async () => {
    try {await navigator.clipboard.writeText(byId('station-code').textContent);byId('station-copy-code').textContent='Código copiado';}
    catch {const range=document.createRange();range.selectNodeContents(byId('station-code'));const selection=getSelection();selection.removeAllRanges();selection.addRange(range);byId('station-copy-code').textContent='Código selecionado — copie manualmente';}
  });
  addEventListener('pagehide', () => {clearCode();form.elements.password.value='';selected=null;});
  addEventListener('pageshow', event => {if(event.persisted)location.reload();});
})();
