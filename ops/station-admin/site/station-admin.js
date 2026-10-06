(() => {
  'use strict';
  const byId = id => document.getElementById(id);
  const config = JSON.parse(document.querySelector('main').dataset.actionInfo);
  const form = byId('station-action-form');
  const support = byId('station-support');
  const registration = byId('station-registration');
  const registrationForm = byId('station-registration-form');
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
  const requestId = () => Array.from(crypto.getRandomValues(new Uint8Array(16)), b => b.toString(16).padStart(2,'0')).join('');
  const grantLabel = kind => ({paid:'Venda paga · R$ 99,90',courtesy:'Cortesia · R$ 0,00',test:'Teste · R$ 0,00'}[kind] || 'Compra Station');
  const eventLabel = event => ({STATION_ADMIN_LICENSE_CREATED:'Licença cadastrada pelo painel',STATION_CODE_ISSUED:'Código emitido',STATION_TRANSFER:'Aparelho liberado',STATION_BLOCK:'Acesso bloqueado',STATION_UNBLOCK:'Acesso desbloqueado',STATION_CANCEL_CODE:'Código cancelado',STATION_REVOKE_SESSION:'Sessão encerrada'}[event] || 'Atualização da licença');
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
        <dl class="station-facts"><div><dt>Licença</dt><dd>${escaped(r.licenseId)}</dd></div><div><dt>Liberação</dt><dd>${escaped(grantLabel(r.grantKind))}</dd></div><div><dt>Pedido ou registro</dt><dd>${escaped(r.sourcePurchaseId || 'Sem pedido vinculado')}</dd></div><div><dt>Aparelho vinculado</dt><dd>${escaped(r.deviceLinked ? `${r.manufacturer} ${r.model}`.trim() : 'Nenhum aparelho vinculado')}</dd></div><div><dt>Último contato do aplicativo</dt><dd>${escaped(date(r.lastContactAt))}</dd></div></dl>
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
    form.elements.request_id.value=requestId();
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
  registration.addEventListener('close', () => {registrationForm.elements.password.value='';});
  support.addEventListener('close', () => {++supportRequest;selected=null;if(changed) location.reload();});
  const clearCode = () => {byId('station-code').textContent='';byId('station-code-expiry').textContent='';};
  const showCode = issued => {
    byId('station-code').textContent=issued.code;
    byId('station-code-expiry').textContent=`Válido até ${issued.expires}`;
    byId('station-code-delivery').textContent=issued.delivery;
    byId('station-copy-code').textContent='Copiar código';
    byId('station-issued').showModal();
  };
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
      if(data.issued) showCode(data.issued);
      else await showSupport(payload.get('license_id'));
    } catch(error) {
      form.elements.password.value='';
      byId('station-action-error').textContent=error.message || 'A resposta não chegou. Confira o histórico antes de repetir.';
      byId('station-action-error').classList.remove('station-hidden');
    } finally {
      busy=false;form.querySelectorAll('button').forEach(button => button.disabled=false);
      byId('station-submit').textContent='Confirmar ação';
    }
  });
  const customerMode = () => {
    const existing=registrationForm.elements.customer_mode.value==='existing';
    byId('station-new-customer').classList.toggle('station-hidden',existing);
    byId('station-existing-customer').classList.toggle('station-hidden',!existing);
    byId('station-new-customer').querySelectorAll('input').forEach(input=>input.disabled=existing);
    registrationForm.elements.customer_id.disabled=!existing;
    registrationForm.elements.customer_id.required=existing;
    registrationForm.elements.allow_additional.disabled=!existing;
    if(!existing)registrationForm.elements.allow_additional.checked=false;
  };
  registrationForm.querySelectorAll('[name=customer_mode]').forEach(input=>input.addEventListener('change',customerMode));
  registrationForm.elements.grant_kind.addEventListener('change', () => {
    const kind=registrationForm.elements.grant_kind.value;
    byId('station-grant-confirm').textContent=kind==='paid'?'Confirmo que recebi o pagamento de R$ 99,90 por esta licença.':kind==='courtesy'?'Autorizo a cortesia sem cobrança para este cliente.':kind==='test'?'Autorizo um acesso de teste sem cobrança, vitalício para um aparelho.':'Confirmo que posso autorizar esta liberação.';
    registrationForm.elements.confirm_grant.checked=false;
    registrationForm.elements.reason.value=kind==='paid'?'Venda Station paga e liberação autorizada pelo administrador.':kind==='courtesy'?'Cortesia Station autorizada pelo administrador para este cliente.':kind==='test'?'Acesso Station de teste autorizado pelo administrador.':'';
  });
  document.querySelectorAll('[data-register-customer]').forEach(button=>button.addEventListener('click', () => {
    if(busy)return;
    registrationForm.reset();registrationForm.elements.request_id.value=requestId();customerMode();
    byId('station-registration-error').classList.add('station-hidden');
    byId('station-registration-support').classList.add('station-hidden');
    byId('station-grant-confirm').textContent='Confirmo que posso autorizar esta liberação.';
    registration.showModal();
  }));
  byId('station-registration-support').addEventListener('click', async () => {
    if(busy)return;
    const id=byId('station-registration-support').dataset.license;
    registration.close();await showSupport(id);
  });
  registrationForm.addEventListener('submit', async event => {
    event.preventDefault();if(busy || !registrationForm.reportValidity())return;
    const payload=new FormData(registrationForm);busy=true;
    const disabled=[...registrationForm.elements].map(element=>[element,element.disabled]);
    disabled.forEach(([element])=>element.disabled=true);
    byId('station-registration-submit').textContent='Cadastrando e gerando código…';
    byId('station-registration-error').classList.add('station-hidden');
    byId('station-registration-support').classList.add('station-hidden');
    try {
      const {data}=await json('/admin/station?format=json',{method:'POST',body:payload});
      registrationForm.elements.password.value='';
      if(!data.ok) {
        byId('station-registration-error').textContent=data.message || 'Não foi possível confirmar o cadastro.';
        byId('station-registration-error').classList.remove('station-hidden');
        if(data.partial){changed=true;notice(data.message,true);}
        if(/^STA-[A-Z0-9_-]{6,64}$/.test(data.licenseId || '')) {
          byId('station-registration-support').dataset.license=data.licenseId;
          byId('station-registration-support').classList.remove('station-hidden');
        }
        return;
      }
      changed=true;notice(data.message);registration.close();showCode(data.issued);
    } catch(error) {
      registrationForm.elements.password.value='';
      byId('station-registration-error').textContent='A resposta não chegou. Mantenha este formulário e tente concluir novamente; ele evita duplicar o cliente e a licença.';
      byId('station-registration-error').classList.remove('station-hidden');
    } finally {
      busy=false;disabled.forEach(([element,wasDisabled])=>element.disabled=wasDisabled);
      byId('station-registration-submit').textContent='Cadastrar e gerar código';
    }
  });
  const copyWithoutClipboardApi = code => {
    // The temporary field must be inside the open dialog; the rest of the page is inert.
    const field=document.createElement('textarea'), previous=document.activeElement;
    field.value=code;field.readOnly=true;field.setAttribute('aria-hidden','true');
    field.style.cssText='position:fixed;left:0;top:0;width:1px;height:1px;opacity:0;';
    byId('station-issued').append(field);
    try {
      field.focus({preventScroll:true});field.select();field.setSelectionRange(0,code.length);
      return document.execCommand('copy');
    } catch {return false;}
    finally {field.remove();previous?.focus({preventScroll:true});}
  };
  byId('station-copy-code').addEventListener('click', async () => {
    const button=byId('station-copy-code'), node=byId('station-code'), code=node.textContent;
    if(!/^[A-Za-z0-9_-]{43}$/.test(code)) {button.textContent='Não há código para copiar';return;}
    button.disabled=true;let copied=false;
    try {
      try {await navigator.clipboard.writeText(code);copied=true;}
      catch {if(node.textContent===code)copied=copyWithoutClipboardApi(code);}
      if(node.textContent!==code)return;
      if(copied)button.textContent='Código copiado';
      else {
        const range=document.createRange();range.selectNodeContents(node);
        const selection=getSelection();selection.removeAllRanges();selection.addRange(range);
        button.textContent='Não foi copiado — copie o texto selecionado';
      }
    } finally {button.disabled=false;}
  });
  addEventListener('pagehide', () => {clearCode();form.elements.password.value='';registrationForm.elements.password.value='';selected=null;});
  addEventListener('pageshow', event => {if(event.persisted)location.reload();});
})();
