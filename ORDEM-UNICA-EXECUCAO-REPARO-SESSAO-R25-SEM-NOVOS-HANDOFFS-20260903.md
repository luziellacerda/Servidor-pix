# ORDEM ÚNICA DE EXECUÇÃO — REPARAR A SESSÃO R25 SEM NOVOS HANDOFFS

Data: 2026-09-03
Responsável pela execução: Codex que opera o servidor de produção
Serviço autorizado para reparo: `turborama-suite-api.service`
Repositório: `https://github.com/luziellacerda/Servidor-pix`
Branch de comunicação: `codex/turborama-suite-vendas-producao-20260828`

## 1. Esta é uma ordem de execução, não outro pedido de análise

Ao receber este arquivo:

1. não criar outro tutorial, plano, solicitação ou handoff;
2. não devolver o trabalho para “outra IA”;
3. permanecer na mesma tarefa até localizar a exceção, corrigir o código/schema necessário, testar, implantar e validar o fluxo real;
4. responder somente com o resultado final executado ou com um bloqueio humano específico que esteja fora da autorização abaixo.

Os commits `f187af67443593d5c341ca3db83b4d9d89328a53` e `5096ad09bdb3129c41d013cf37fe27529c5169dd` adicionaram apenas documentação. Eles não corrigiram o serviço. Não repetir esse comportamento.

## 2. Limite de autorização humana

O proprietário informa que houve troca de código e/ou chaves sem autorização humana. A partir desta ordem, fica expressamente determinado:

### Está autorizado

- fazer backup recuperável do release e das referências de configuração atuais;
- inspecionar logs, metadados de banco, schema, grants, triggers, constraints, unit files, drop-ins, hashes e commits;
- corrigir exclusivamente o erro de sessão do `turborama-suite-api.service`;
- adicionar diagnóstico interno sanitizado;
- corrigir código, teste e migration idempotente se a evidência mostrar necessidade;
- compilar um release novo, implantar atomicamente, reiniciar somente o serviço necessário e executar rollback se a validação falhar;
- criar commit contendo a correção real, os testes e, quando indispensável, a migration — não apenas documentação.

### Não está autorizado

- gerar, substituir, rotacionar, importar, remover ou publicar qualquer chave privada ou pública;
- alterar `authority.json`, envelopes públicos, key IDs, TLS pins, certificados, peppers ou arquivos de cerimônia;
- alterar domínio, bytes do proof, `contextHash`, ProductId ou contratos `/v1`;
- recriar, revogar, transferir ou editar a licença ativa;
- trocar DeviceId, fingerprint, chave CNG ou perfil da máquina;
- gerar novo OTP, alterar preço/venda/pagamento ou mexer nos serviços PIX, painel, catálogo ou gateway sem causa técnica comprovada;
- copiar segredos para Git, relatório, terminal compartilhado ou chat.

Qualquer possível rotação/restauração de chave exige autorização humana nova e explícita. Ela não faz parte deste reparo. As chaves atuais já passaram pelo challenge, portanto não devem ser tocadas para corrigir o `500`.

## 3. Fatos técnicos já comprovados

Cliente:

- `LicenseId`: `TS-4F5E95A6D70CF60D322D9CF2915AC70F`;
- perfil: `SOFTWARE_BOUND_ONLINE`;
- `DeviceId`: `8e808a5b156b758aeea5aac573eaec94537778982f030ea16431a562ba6bc599`;
- `HardwareFingerprint`: `3af8338625225ee6f90883a2e66ffd2aa02bdfb2b2bbfb20899e5e59a3daa24e`;
- commit do cliente: `c2d41a96417454e715ee666a58c3c04727079c4d`;
- SHA-256 do EXE: `dd0a83a695a7a20209335238c7e24d2e98a4e179f7dbf5336e0d047a1b652b02`;
- autoridade pública e pin TLS foram validados;
- a mesma licença/máquina já teve sessão e inventário aceitos em 2026-09-02 15:38:40 -03.

Falha atual reproduzida:

- correlação do cliente: `r25diag-20260903014038-6d5ca5ac`;
- `POST /v1/suite/challenges` -> HTTP `200`;
- `POST /v1/suite/sessions` -> HTTP `500 INTERNAL_ERROR`.

Correlação informada pelo próprio servidor:

- `6f408eba0eed94a1e7dd5316e28cd278`;
- mesmo resultado: challenge `200`, session `500`.

Estado informado pelo servidor:

- release ativo: `r25-1-inventory-whatsapp-20260902`;
- licença e dispositivo `ACTIVE`;
- migrations `017_suite_commerce_session_permissions` e `020_suite_device_inventory_r25` registradas como aplicadas;
- tabelas de sessões, entregas, presença e outbox existentes;
- grants declarados como presentes;
- existe uma sessão antiga expirada, que deve ser preservada e usada no teste de regressão.

Conclusão obrigatória: TLS, autoridade, licença e dispositivo passam pela primeira etapa. A falha está no processamento de `/v1/suite/sessions`. Não recompilar o cliente e não reautorizar a máquina.

## 4. Reparar diretamente o serviço correto

Trabalhar somente no caminho efetivamente carregado por `turborama-suite-api.service`.

### Etapa A — congelar e identificar o runtime real

1. Registrar, sem exibir valores secretos:
   - `systemctl cat turborama-suite-api.service`;
   - `systemctl show` para `ExecStart`, `WorkingDirectory`, unit/drop-ins e caminhos de EnvironmentFile;
   - caminho resolvido do DLL ativo;
   - SHA-256 do DLL ativo;
   - release/diretório ativo;
   - commit-fonte usado para compilá-lo, se houver manifesto.
2. Fazer backup do release ativo e das referências de configuração, mantendo segredos fora do Git e do relatório.
3. Comparar o DLL/release implantado com o código-fonte que será corrigido. O branch de documentação não deve ser presumido como fonte do DLL ativo.
4. Se houver divergência entre fonte e runtime, corrigir a partir da fonte exata do release ativo ou incorporar essa fonte ao Git antes de editar. Não implantar código antigo sobre o R25.

### Etapa B — tornar a exceção observável internamente

No mapeamento de `/v1/suite/sessions` em `src/TurboRamaSuiteOnlineServer/Program.cs`, o `catch (Exception)` atual descarta a exceção e registra apenas a correlação. Corrigir isso antes de tentar adivinhar a causa.

Implementar log interno restrito e estruturado:

- correlação;
- método e rota;
- tipo da exceção;
- para `PostgresException`: `SqlState`, schema, tabela, coluna, constraint e routine quando disponíveis;
- stack trace no journal interno restrito;
- nenhuma senha, DSN, chave, OTP, proof, cookie, corpo integral da requisição ou valor de parâmetro SQL.

Manter a resposta pública exatamente sanitizada: HTTP `500`, código `INTERNAL_ERROR`, sem SQL ou stack para o cliente.

Adicionar teste que garanta simultaneamente:

- o detalhe interno é registrado;
- a resposta externa continua sanitizada;
- nenhum segredo é incluído no log.

Somente melhorar o log não conclui esta ordem. Usar o log para encontrar e reparar a causa real.

### Etapa C — reproduzir e isolar a instrução exata

Reproduzir `/v1/suite/sessions` no release instrumentado e acompanhar a correlação. Inspecionar, na ordem real de `PostgresSuiteStore.CompleteSessionAsync`:

1. lock/leitura de `suite.suite_licenses` e `provisioning_origin`;
2. leitura/lock de `suite.suite_license_deliveries` quando a origem for `COMMERCE`;
3. lock de `suite.suite_license_enrollments`;
4. lock de `suite.suite_devices`;
5. upsert em `suite.suite_sessions`;
6. CTE/upsert de `suite.suite_device_presence`;
7. inserção idempotente em `suite.suite_connection_notification_outbox`;
8. consumo do challenge;
9. commit da transação serializável;
10. assinatura da assertion de sessão.

Executar a verificação com o papel e o banco realmente usados pelo processo, não apenas com `postgres` ou com o papel que se imagina estar configurado.

Além do registro em `schema_migrations`, comparar o schema vivo com a migration oficial:

- nomes, tipos, nulabilidade e defaults das colunas;
- índices e alvos de `ON CONFLICT`;
- PKs, FKs e checks;
- triggers e funções acionadas;
- owner e grants efetivos, inclusive privilégios de sequência;
- `search_path` e banco/schema reais do serviço.

Usar a sessão antiga expirada no teste. O caso obrigatório é: licença e dispositivo ativos, linha anterior expirada para o mesmo par `(license_id, device_id)`, seguida por `session.open`. O upsert deve renovar a linha e retornar `200`, sem colisão da constraint única de `session_id`.

Também testar presença inexistente e já existente, outbox vazia e evento duplicado. Falha posterior do worker WhatsApp não pode invalidar nem impedir a sessão; não realizar chamada externa ao WhatsApp dentro da requisição de sessão.

### Etapa D — aplicar a correção baseada na evidência

Aplicar exatamente uma destas classes de reparo conforme o erro capturado:

- schema drift: migration idempotente e revisada que reconcilie somente o objeto divergente;
- papel runtime sem permissão: grant mínimo e explícito por migration;
- SQL incompatível com constraint/coluna: corrigir a instrução e cobrir com teste PostgreSQL;
- nulo/estado legado não tratado: compatibilizar o dado legado sem relaxar autorização;
- colisão no upsert da sessão expirada: corrigir a semântica de conflito preservando uma sessão ativa por dispositivo;
- erro na presença/outbox: corrigir a operação transacional/idempotente; não remover presença/outbox como atalho;
- DLL divergente: compilar e implantar a fonte R25 correta com a correção;
- erro de assinatura após o commit: corrigir apenas carregamento/uso da autoridade já aprovada, sem criar ou trocar chave.

Não editar manualmente a licença para fazer o teste passar. Não apagar a sessão antiga sem demonstrar que ela é corrupção e sem backup.

## 5. Testes obrigatórios antes do deploy

Adicionar e executar testes automatizados para:

1. sessão nova;
2. renovação sobre sessão expirada existente;
3. heartbeat;
4. retry/serialização e deadlock;
5. presença criada e atualizada;
6. outbox idempotente sem duplicação;
7. licença/dispositivo inválidos retornando `403`;
8. challenge consumido/repetido retornando `409`;
9. falha interna retornando `500` sanitizado e log interno útil;
10. nenhuma regressão nos endpoints de ativação e inventário.

Executar também os testes PostgreSQL/integrados contra schema equivalente ao de produção. Teste unitário sem banco não basta para este defeito.

## 6. Commit, deploy e validação final

1. Revisar o diff e confirmar que não há mudança de chave, autoridade, licença, protocolo, preço ou serviço fora do escopo.
2. Criar commit com código/testes/migration reais. Um commit contendo apenas `.md` não é aceite.
3. Compilar Release reproduzível em diretório novo.
4. Registrar commit e SHA-256 dos binários.
5. Implantar atomicamente em novo release, preservando o anterior para rollback.
6. Reiniciar `turborama-suite-api.service` e verificar health/readiness.
7. Executar o fluxo assinado real com a licença e o dispositivo atuais.
8. Se for indispensável uma ação no PC cliente, pedir apenas uma vez, na própria tarefa, para clicar com OTP vazio enquanto o servidor monitora. Não criar novo handoff.

Critério obrigatório de aceite:

- `/v1/suite/challenges` -> `200`;
- `/v1/suite/sessions` -> `200`;
- assertion de sessão válida no cliente;
- sessão `ACTIVE` para o dispositivo correto;
- challenge consumido uma vez;
- presença `ONLINE` gravada corretamente;
- no máximo um evento idempotente na outbox;
- tentativa repetida/replay tratada pelo código previsto;
- health/readiness saudáveis;
- nenhuma chave/autoridade/licença alterada;
- nenhum segredo exposto em Git ou logs compartilhados.

Se qualquer item falhar, executar rollback imediatamente, corrigir na mesma tarefa e repetir. Não declarar conclusão parcial.

## 7. Forma do retorno — sem novo handoff

Não criar outro arquivo `.md` de encaminhamento. Responder na própria tarefa do servidor com:

- causa-raiz exata e o ponto que falhava;
- SQLSTATE/constraint/tabela sanitizados, quando aplicável;
- commit da correção real;
- release e hash efetivamente implantados;
- correlação e horários do teste final;
- prova `challenges=200` e `sessions=200`;
- estado final da sessão, presença e outbox;
- confirmação de que chaves, autoridades, licença e DeviceId permaneceram inalterados;
- localização do backup e comando de rollback, sem segredos.

Não responder com “recomenda-se”, “a outra IA deve”, “falta investigar” ou outro tutorial. Esta ordem só termina com o serviço reparado e validado, ou com um bloqueio humano específico fora do escopo autorizado.
