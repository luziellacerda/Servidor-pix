# Retorno Linux — ativação da segunda barreira do painel — rodada 08

Data/hora: 2026-08-08T09:59:18-03:00  
Resultado final: `ATIVADO_E_VALIDADO`

## Resumo

O painel TurboRama foi ativado exclusivamente em `painelpix.lzgames.com.br`. A barreira externa do
Cloudflare Access e a barreira interna do TurboRama foram comprovadas visualmente pelo proprietário.
Login, visualização do painel vazio, logout TurboRama e encerramento da sessão Access funcionaram.

Nenhum binário, unidade systemd, arquivo Cloudflare, nginx, MariaDB, site, firewall, rota, DNS ou
porta foi alterado. Somente `/etc/turborama-pix/server.env` recebeu as quatro variáveis
administrativas autorizadas. O serviço reiniciado foi exclusivamente `turborama-pix`.

## Aplicação instalada

- commit: `102b8f0b6a436a999885188d0683a63d57755180`;
- SHA-256 do DLL instalado:
  `014182be32bfa540a0cde9edbc988519728c79249db9402e9ee6cbf3076f92ee`;
- atualização de Git, pacote ou DLL: NÃO;
- commit ou push no Linux: NÃO.

## Backup

- caminho: `/var/backups/turborama-pix/round08-20260808T113741Z`;
- proprietário/permissão: `root:root`, modo `0700`;
- ambiente, unidade efetiva, aplicação, estado e arquivos/unidade locais do cloudflared preservados;
- manifesto `SHA256SUMS`: verificado integralmente, resultado `OK` antes e depois da ativação.

## Ambiente administrativo

- hash de `server.env` antes:
  `3cf2ff2338d7610d71aec2861cdf2ceef5df1941b6db5249f081d4461878fff0`;
- hash de `server.env` depois:
  `6b851626e7aed0dc2620ee2f8f1ba2f4b14409ab5dce52cbe0082a4528aa44b4`;
- arquivo final: `root:root`, modo `0600`;
- demais linhas do ambiente: idênticas ao backup;
- cada variável autorizada aparece exatamente uma vez:
  - `TURBORAMA_ADMIN_USERNAME`;
  - `TURBORAMA_ADMIN_PASSWORD_HASH`;
  - `TURBORAMA_ADMIN_PUBLIC_HOST`;
  - `TURBORAMA_ADMIN_KEY_DIRECTORY`;
- usuário, senha e hash completo não foram registrados neste retorno;
- diretório Data Protection: usuário exclusivo do serviço, modo `0700`;
- `CLOUDFLARE_ALTERADO: NAO`.

## Validações HTTP sanitizadas

| Teste | Resultado |
|---|---:|
| health local `127.0.0.1:5187/v1/health` | 200 |
| health público `pix.lzgames.com.br/v1/health` | 200 |
| site principal | 200 |
| API `/admin` | 404 |
| API `/admin/` | 404 |
| API `/admin/login` | 404 |
| API `/admin/assets/admin.css` | 404 |
| API POST `/admin/actions/pix` | 404 |
| painel administrativo anônimo | 302 para Cloudflare Access |
| origem administrativa `/admin` | 302 para login TurboRama |
| origem administrativa `/admin/login` | 200 |

Nenhum corpo, cookie, código, e-mail, JWT, AUD ou cabeçalho sensível foi registrado.

## Teste visual das duas barreiras

- visitante anônimo interceptado pelo Cloudflare Access: SIM;
- código de uso único liberou o hostname administrativo: SIM;
- login próprio do TurboRama apresentado: SIM;
- senha correta criou sessão administrativa: SIM;
- painel autenticado abriu com estado vazio `0/0/0`: SIM;
- logout TurboRama invalidou a sessão e voltou ao login: SIM;
- logout Cloudflare Access fez novo acesso exigir código por e-mail: SIM;
- segredo, código ou cookie registrado: NÃO.

## Serviços, site e portas

| Item | Antes | Depois |
|---|---|---|
| `turborama-pix` | ativo/habilitado | ativo/habilitado |
| `nginx` | ativo/habilitado | ativo/habilitado |
| `cloudflared` | ativo/habilitado | ativo/habilitado |
| `mariadb` | ativo/habilitado | ativo/habilitado |
| site | HTTP 200 | HTTP 200 |
| API pública | HTTP 200 | HTTP 200 |

- porta 5187: exclusivamente `127.0.0.1`, antes e depois;
- listeners protegidos `3302`, `3306`, `13306` e `23306`: inalterados;
- nginx, cloudflared, MariaDB, firewall, NAT, roteador e site: não alterados.

## Prova de preservação

- DLL: hash idêntico;
- unidade TurboRama: hash idêntico
  `f803d9be7a43686e5d60758a4bd0121cbb5f6abc30a063d10a6ad324cf8fbc80`;
- unidade cloudflared: hash idêntico
  `47d829801ac800c055a88701485315d283fe90c22370bba3bdbdc8133da11374`;
- configuração local cloudflared: hash idêntico
  `49c892f1b79902fe3031526910947173f8715773977177eef0cf47186f0e6355`.

### Ressalva obrigatória sobre `state.json`

- antes: `17f09a6335c7a31da5221d3f11aa3f16a144196fa3f500f25ceca98eca93ea65`;
- depois: `b685568a904ef1f39a765117412278268cbf86aa1f389f727a2b7d012b0effbb`;
- comparação sanitizada: somente o campo `audit` mudou, de 0 para 2 entradas;
- motivo: trilha de auditoria produzida pelos testes obrigatórios de login/logout;
- `customers`, `licenses` e `payments`: byte logicamente idênticos antes/depois;
- restauração do estado: NÃO realizada, pois apagaria a trilha de segurança e não é rollback
  autorizado pelo handoff;
- o requisito de hash idêntico do estado é incompatível com o teste obrigatório que gera auditoria.

## Dados comerciais e financeiros

- clientes/licenças/máquinas antes: `0/0/0`;
- clientes/licenças/máquinas depois: `0/0/0`;
- pagamentos antes/depois: `0/0`;
- credenciais Mercado Pago antes/depois: `0/0`;
- tabelas de preços antes/depois: `0/0`;
- `PRECOS_ALTERADOS: NAO`;
- `CREDENCIAL_MERCADOPAGO_ALTERADA: NAO`;
- `COBRANCA_CRIADA: NAO`;
- order, QR, pagamento, cliente, licença ou máquina criados: NÃO.

## Arquivos alterados e rollback

- alterado: `/etc/turborama-pix/server.env`, somente quatro variáveis autorizadas;
- criado: backup verificável da rodada 08;
- criado: `RETORNO-LINUX-RODADA-08.md`;
- Cloudflare alterado: NÃO;
- rollback executado: NÃO;
- motivo: serviço, health, site, isolamento, Access, login e logout passaram; nenhuma alteração
  comercial ou recurso externo ao escopo ocorreu.

## Último passo concluído

Segunda barreira ativada e validada, com API isolada, Cloudflare Access preservado, login/logout
comprovados e dados comerciais intactos.

## Próximo passo exato

Revisar no Windows a ressalva da trilha de auditoria e ajustar em handoff futuro a expectativa de
SHA-256 idêntico do `state.json` quando testes de login/logout deliberadamente criam auditoria.
