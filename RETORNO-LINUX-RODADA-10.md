# Retorno Linux — aceitar PIX da versão Windows — rodada 10

Data/hora: 2026-08-10T18:49:18-03:00  
Resultado: `SERVIDOR_PRONTO_PARA_RECEBER_PIX: SIM`

## Verificações

| Item | Resultado |
|---|---|
| `turborama-pix` | ativo e habilitado |
| `nginx` | ativo e habilitado |
| `cloudflared` | ativo e habilitado |
| `mariadb` | ativo e habilitado |
| health local `127.0.0.1:5187/v1/health` | HTTP 200 |
| health público `pix.lzgames.com.br/v1/health` | HTTP 200 |
| site principal | HTTP 200 |
| painel administrativo anônimo | HTTP 302, interceptado pelo Cloudflare Access |
| API `/admin` | HTTP 404 |
| API `/admin/` | HTTP 404 |
| API `/admin/login` | HTTP 404 |
| API `/admin/assets/admin.css` | HTTP 404 |
| API `/admin/assets/admin.js` | HTTP 404 |

- aplicação atendendo exclusivamente em `127.0.0.1:5187`: SIM;
- porta 5187 exposta externamente: NÃO;
- listeners protegidos `3302`, `3306`, `13306` e `23306`: inalterados;
- site, Cloudflare Tunnel/Access, nginx, MariaDB, firewall, NAT e roteador: não alterados;
- serviço reiniciado nesta rodada: NÃO;
- arquivo Linux trocado nesta rodada: NÃO;
- executável Windows copiado para o Linux: NÃO.

## Contagens comerciais

Ordem: clientes/licenças/máquinas/pagamentos/credenciais Mercado Pago/tabelas de preços.

- antes: `1/1/1/0/0/0`;
- depois: `1/1/1/0/0/0`;
- divergência durante a rodada: NÃO;
- a máquina já estava vinculada à licença existente antes desta auditoria;
- `LICENCA_CRIADA: NAO`;
- máquina criada nesta rodada: NÃO;
- `COBRANCA_CRIADA: NAO`;
- `CREDENCIAL_MERCADOPAGO_ALTERADA: NAO`;
- preços alterados: NÃO;
- pagamento, order ou QR criado: NÃO.

## Rollback

- rollback: `NAO_APLICAVEL`;
- motivo: auditoria exclusivamente de leitura, sem alteração de arquivo, serviço ou estado.

## Resultado

Servidor Linux confirmado saudável, isolado e pronto para receber chamadas PIX do agente Windows.
Nenhuma divergência foi encontrada durante a rodada 10.

## Próximo passo

No Windows, testar os binários na instalação de teste do gabinete. Qualquer teste real do Mercado
Pago deve ocorrer somente com credenciais revogáveis controladas pelo proprietário, sem transportar
segredos para conversa ou para o servidor por este handoff.
