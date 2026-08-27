# RETORNO LINUX — RODADA 12

- Data/hora inicial: `2026-08-14T08:29:00-03:00`
- Data/hora final: `2026-08-14T08:36:55-03:00`
- Resultado: `INTERROMPIDO NA FASE 1 — SEM ALTERAR O SERVIDOR`
- Último passo concluído: auditoria somente leitura dos serviços, unit, runtime, arquivos instalados, endpoints e listener; a auditoria do estado privado não pôde ser concluída.
- Divergência/bloqueio: o usuário da sessão não possui acesso a `/etc/turborama-pix/server.env` nem aos arquivos de `/var/lib/turborama-pix`; `sudo` não interativo exige senha. Portanto, não foi possível comprovar contagens, último contato, hash autenticado do estado nem criar/comparar o backup verificável. O critério de parada do handoff foi aplicado antes da Fase 2.

## Repositório e artefato

- Branch local/remota: `main` / `origin/main`
- Commit disponível e sincronizado: `a3bb0f20da79cc9ffe4779d2ab0526e53c952914`
- Commit implantado: `NAO IMPLANTADO`
- O commit contém o handoff da Rodada 12 e alterações em `AdminPanel.cs`, `OnlineServerSelfTest.cs`, `ServerCore.cs`, `README.md`, `deploy/linux/README.md` e `COMPILAR-SERVIDOR-PIX-ONLINE.ps1`: `SIM`
- ZIP — caminho rastreado: `outputs/TurboRamaPixOnlineServer-portable-RODADA12-20260814.zip`
- ZIP — tamanho e SHA-256 calculados nesta execução: `NAO VALIDADO; FASE 2 NAO INICIADA`
- ZIP — valores esperados no handoff: `104376 bytes`; `FF0C82974AD721B6F7007CE1C56CE2A1402FF78B4A75A34EDBDC19D7CFD63C81`
- DLL do pacote — tamanho e SHA-256 calculados nesta execução: `NAO VALIDADO; FASE 2 NAO INICIADA`
- DLL do pacote — valores esperados no handoff: `309760 bytes`; `4C5A51D8B547FEC90CB276737BCC44BE476434BDA9EFCDFBCAA342CC86EFA43F`
- Resultado do autoteste: `NAO EXECUTADO; FASE 2 NAO INICIADA`

## Serviço e runtime antes

- Runtime: `.NET 8.0.29`, `Microsoft.NETCore.App 8.0.29`, `Microsoft.AspNetCore.App 8.0.29`, `ubuntu.24.04-x64`
- Unit: `/etc/systemd/system/turborama-pix.service`
- Usuário/grupo: `turborama-pix:turborama-pix`
- Diretório de trabalho: `/opt/turborama-pix`
- Comando: `/usr/bin/dotnet /opt/turborama-pix/TurboRamaPixOnlineServer.dll`
- Ambiente referenciado: `/etc/turborama-pix/server.env` (conteúdo não lido nem exibido)
- `turborama-pix`: `active`, `enabled`, `NRestarts=0`
- `nginx`: `active`, `enabled`
- `cloudflared`: `active`, `enabled`
- `mariadb`: `active`, `enabled`

## Aplicação instalada antes

| Arquivo | Tamanho | SHA-256 | Proprietário/permissão |
|---|---:|---|---|
| `CHECKSUMS-SHA256.txt` | 432 bytes | `bb6dc4e84c6177632e6e535476d6f880e398d572e6711c3a9ee19a88518443f5` | `root:root 0644` |
| `TurboRamaPixOnlineServer.deps.json` | 464 bytes | `ec658280d8716310532b713ea284c78f8c1dbbba0a3e5bcfb953ba8095cbadab` | `root:root 0644` |
| `TurboRamaPixOnlineServer.dll` | 249344 bytes | `4c49b67a7ae719def39554b1064d71d0239f9b9bf5eb1c96bcff95b3644749a2` | `root:root 0644` |
| `TurboRamaPixOnlineServer.staticwebassets.endpoints.json` | 53 bytes | `c1686417e8d5c31bba969f12b6f3d2b35e6609adb5ca42f212261a3a02771aac` | `root:root 0644` |
| `TurboRamaPixOnlineServer.runtimeconfig.json` | 536 bytes | `a9af57db55e6df5de551cd6ccc9d607872d87470124c3141916079f4f00b7f76` | `root:root 0644` |

## Verificações antes

- Health local `http://127.0.0.1:5187/v1/health`: `HTTP 200`
- Health público `https://pix.lzgames.com.br/v1/health`: `HTTP 200`
- Site principal `https://app.lzgames.com.br/`: `HTTP 200`
- Painel anônimo: `HTTP 302` para login do Cloudflare Access; nenhum cookie ou token foi registrado neste retorno.
- API `https://pix.lzgames.com.br/admin`: `HTTP 404`
- API `https://pix.lzgames.com.br/admin/`: `HTTP 404`
- API `https://pix.lzgames.com.br/admin/login`: `HTTP 404`
- Listener `5187`: somente `127.0.0.1:5187`
- Contagens de clientes, licenças, máquinas, sessões, pagamentos, credenciais e preços: `NAO OBTIDAS — ACESSO AO ESTADO NEGADO`
- Status e último contato da licença/máquina existente: `NAO OBTIDOS — ACESSO AO ESTADO NEGADO`
- Hash autenticado/backup verificável do estado: `NAO OBTIDO — ACESSO AO ESTADO NEGADO`

## Backup

- Caminho do backup: `NAO CRIADO`
- Manifesto do backup: `NAO CRIADO`
- Motivo: a Fase 1 não foi aprovada e a sessão não possui privilégio para ler/copiar os arquivos privados exigidos.

## Verificações depois

- Aplicação depois: `NAO APLICAVEL — NENHUMA TROCA REALIZADA`
- Status dos quatro serviços depois: `NAO REINICIADOS NEM ALTERADOS`
- Health local/público/site depois: `NAO APLICAVEL — IMPLANTACAO NAO INICIADA`
- Isolamento `/admin*`, proteção Access e listener depois: `NAO APLICAVEL — IMPLANTACAO NAO INICIADA`
- Hash/contagens comerciais antes/depois: `NAO COMPARADOS — ESTADO PRIVADO INACESSIVEL`
- Indicadores, filtros, confirmações e exportação CSV: `NAO VERIFICADOS EM PRODUCAO — ACEITE NAO INICIADO`
- Ausência de formulários de preço e Mercado Pago: `NAO VERIFICADA EM PRODUCAO — ACEITE NAO INICIADO`

## Declarações obrigatórias

- `TRANSFERENCIA_EXECUTADA: NAO`
- `CODIGO_ATIVACAO_GERADO: NAO`
- `LICENCA_ALTERADA: NAO`
- `MAQUINA_ALTERADA: NAO`
- `SESSAO_ALTERADA: NAO`
- `CREDENCIAL_MERCADOPAGO_ALTERADA: NAO`
- `COBRANCA_CRIADA: NAO`
- `ROLLBACK: NAO_APLICAVEL`
- `SERVIDOR_RODADA_12_PAINEL_PROFISSIONAL_E_ESTAVEL: NAO`

Nenhum serviço foi parado ou reiniciado. Nenhum arquivo de `/opt/turborama-pix`, `/etc/turborama-pix` ou `/var/lib/turborama-pix` foi alterado. Nenhuma configuração de site, Cloudflare, nginx, MariaDB, firewall, porta ou sistema operacional foi alterada.

## Retomada controlada — 2026-08-14

- Data/hora final da retomada: `2026-08-14T11:35:50-03:00`
- Commit validado: `a3bb0f20da79cc9ffe4779d2ab0526e53c952914`
- ZIP validado: `104376 bytes`; SHA-256 `ff0c82974ad721b6f7007ce1c56ce2a1402ff78b4a75a34edbdc19d7cfd63c81`
- DLL do pacote validada: `309760 bytes`; SHA-256 `4c5a51d8b547fec90cb276737bcc44be476434bda9efcdfbcaa342cc86efa43f`
- Forma do pacote: cinco arquivos; checksums internos aprovados; nenhum tipo proibido encontrado.
- Autoteste temporário: código 0 e mensagem `SELF-TEST SERVIDOR ONLINE: OK`.
- Auditoria privada aprovada: 1 cliente, 1 licença, 1 máquina ativa, 1 sessão, nenhum pagamento, nenhuma credencial Mercado Pago e nenhuma configuração de preços.
- Backup verificável: `/var/backups/turborama-pix/round12-20260814T143339Z` com `SHA256SUMS` e `SHA256SUMS.verify`.
- A nova DLL iniciou e respondeu health local HTTP 200, mas o hash do estado divergiu durante o aceite.
- Critério de parada aplicado: a nova aplicação foi preservada em `/opt/turborama-pix.round12-failed-20260814T143339Z`; aplicação e estado anteriores foram restaurados.
- DLL restaurada: `249344 bytes`; SHA-256 `4c49b67a7ae719def39554b1064d71d0239f9b9bf5eb1c96bcff95b3644749a2`.
- Depois do rollback: quatro serviços ativos; `NRestarts=0`; health local/público e site HTTP 200.
- Painel profissional não foi aceito em produção porque a divergência ocorreu antes da Fase 5.
- Último passo concluído: rollback automático e validação operacional posterior.
- Divergência: a Rodada 12 altera o estado persistido ao iniciar, contrariando a exigência de hash comercial idêntico. Nova tentativa proibida sem artefato corrigido ou migração explícita validada.

### Declarações finais da retomada

- `TRANSFERENCIA_EXECUTADA: NAO`
- `CODIGO_ATIVACAO_GERADO: NAO`
- `LICENCA_ALTERADA: NAO`
- `MAQUINA_ALTERADA: NAO`
- `SESSAO_ALTERADA: NAO`
- `CREDENCIAL_MERCADOPAGO_ALTERADA: NAO`
- `COBRANCA_CRIADA: NAO`
- `ROLLBACK: EXECUTADO_COM_SUCESSO`
- `SERVIDOR_RODADA_12_PAINEL_PROFISSIONAL_E_ESTAVEL: NAO`
