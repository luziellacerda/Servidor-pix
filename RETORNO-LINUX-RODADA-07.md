# Retorno Linux — publicação do painel Cloudflare — rodada 07

Data/hora: 2026-08-07T17:17:04-03:00  
Resultado final: `BLOQUEADO_POR_VALIDACAO_ACCESS`

## Motivo da parada

A auditoria local e do conector foi concluída sem alterações. O túnel existente é gerenciado
remotamente pelo Cloudflare: o conector recebe configuração remota com mais hostnames que o arquivo
local. O hostname `painelpix.lzgames.com.br` ainda não possui DNS nem rota.

Esta sessão Linux não possui acesso autenticado ao painel/API Cloudflare que permita:

- abrir e auditar independentemente a aplicação Access `painelpix`;
- confirmar ausência de `Everyone`, `Bypass`, curingas e exceções;
- confirmar política, regra de conta, provedor e duração;
- obter privadamente o `teamName` e o AUD correto;
- associar inequivocamente o AUD à aplicação `painelpix`;
- habilitar e comprovar `Protect with Access` e validação JWT obrigatória na nova rota.

Como esses itens são condições obrigatórias anteriores à publicação, nenhuma rota, DNS, variável
administrativa, senha ou backup de pré-alteração foi criado.

## Aplicação instalada

- commit: `102b8f0b6a436a999885188d0683a63d57755180`;
- binário atualizado nesta rodada: NÃO;
- SHA-256 do DLL instalado:
  `014182be32bfa540a0cde9edbc988519728c79249db9402e9ee6cbf3076f92ee`;
- rodada instalada: 06;
- painel: fechado por padrão.

## Cloudflare Access

- `ACCESS_APP: painelpix` — existência/configuração informada pelo handoff, mas não auditada
  independentemente nesta sessão;
- política: `Turborama`, Allow, uma identidade exata e requisito de conta — NÃO VERIFICADO;
- quantidade de regras: NÃO VERIFICADA;
- duração de 30 minutos: NÃO VERIFICADA;
- ausência de Everyone/Bypass/curinga: NÃO VERIFICADA;
- `PROTECT_WITH_ACCESS: NAO` — nenhuma rota foi criada;
- `JWT_VALIDATION_REQUIRED: NAO` — nenhuma rota foi criada;
- AUD correto configurado: NÃO;
- `teamName` obtido: NÃO;
- nenhum e-mail, AUD, token ou credencial foi exibido.

## Túnel, rota e DNS

- túnel existente: preservado;
- modo de gerenciamento: REMOTO;
- conector: ativo;
- arquivo local do cloudflared: válido, resultado `OK`;
- configuração local convertida ou editada: NÃO;
- configuração remota alterada: NÃO;
- rota `painelpix.lzgames.com.br` criada: NÃO;
- DNS criado: NÃO;
- hostname resolvível no momento da auditoria: NÃO;
- destino planejado: serviço HTTP em loopback, porta 5187;
- regras existentes reordenadas/substituídas: NÃO;
- rollback de rota necessário: NÃO, pois não houve alteração.

## Backup

- backup da rodada 07 criado: NÃO;
- manifesto da rodada 07: NÃO APLICÁVEL;
- motivo: o critério de parada ocorreu na Fase A, antes da Fase B e de qualquer alteração;
- backup anterior preservado:
  `/var/backups/turborama-pix/round06-20260807T173720Z`;
- versão anterior e materiais isolados: preservados.

## Testes Access/painel

- teste não autenticado no novo hostname bloqueado: NÃO TESTADO, hostname sem DNS/rota;
- teste autorizado chegou ao 404 antes da ativação: NÃO TESTADO;
- `TURBORAMA_ADMIN_PUBLIC_HOST: VAZIO` — variável ausente;
- login TurboRama testado: NÃO;
- logout TurboRama testado: NÃO;
- logout Access testado: NÃO;
- `PAINEL_PUBLICO: NAO`;
- hostname administrativo publicado: NÃO;
- cookies, CSRF, JWT e cabeçalhos Access capturados: NÃO.

## Isolamento da API preservado

| Caminho em `pix.lzgames.com.br` | Método | HTTP | Redirecionamento |
|---|---|---:|---|
| `/admin` | GET | 404 | não |
| `/admin/` | GET | 404 | não |
| `/admin/login` | GET | 404 | não |
| `/admin/assets/admin.css` | GET | 404 | não |
| `/admin/actions/pix` | POST | 404 | não |
| `/v1/health` | GET | 200 | não |

## Integridade antes/depois

Nenhum hash mudou durante a rodada:

- `state.json`:
  `17f09a6335c7a31da5221d3f11aa3f16a144196fa3f500f25ceca98eca93ea65`;
- `server.env`:
  `3cf2ff2338d7610d71aec2861cdf2ceef5df1941b6db5249f081d4461878fff0`;
- unidade `turborama-pix`:
  `f803d9be7a43686e5d60758a4bd0121cbb5f6abc30a063d10a6ad324cf8fbc80`;
- unidade cloudflared:
  `47d829801ac800c055a88701485315d283fe90c22370bba3bdbdc8133da11374`;
- configuração local cloudflared:
  `49c892f1b79902fe3031526910947173f8715773977177eef0cf47186f0e6355`.

Justificativa de mudança do ambiente: NÃO APLICÁVEL; ambiente inalterado.

## Estado final

| Item | Estado |
|---|---|
| `turborama-pix` | ativo e habilitado |
| `nginx` | ativo e habilitado |
| `cloudflared` | ativo e habilitado |
| `mariadb` | ativo e habilitado |
| health local PIX | saudável |
| health público PIX | HTTP 200 |
| site principal | HTTP 200 |

As portas `3302`, `3306`, `13306`, `23306` e o listener loopback 5187 permaneceram
inalterados. Firewall, NAT, nginx, MariaDB, site, aplicações LZGames, API e regras do túnel não foram
modificados.

## Dados comerciais e financeiros

- clientes/licenças/máquinas: `0/0/0`;
- preços alterados: `NAO`;
- `CREDENCIAL_MERCADOPAGO_ALTERADA: NAO`;
- `COBRANCA_CRIADA: NAO`;
- licença, ativação, sessão, order, QR ou pagamento criado: NÃO.

## Arquivos criados ou alterados

- criado somente:
  `/home/lz-servidor/turborama-download/Servidor-pix/RETORNO-LINUX-RODADA-07.md`;
- nenhuma configuração do servidor ou Cloudflare foi alterada;
- nenhum commit, push, pacote ou binário foi modificado.

## Comandos somente leitura executados

```bash
leitura integral de RETORNO-LINUX-RODADA-06.md
git rev-parse HEAD
sha256sum do DLL, estado, ambiente e unidades/configuração
systemctl is-active/is-enabled dos serviços
curl dos endpoints, site e cinco rotas administrativas
ss -lnt com filtro das portas protegidas
cloudflared ingress validate
leitura sanitizada da configuração local
systemctl show cloudflared
journalctl sanitizado das versões de configuração remota
getent ahosts do hostname planejado
cloudflared tunnel info do túnel existente
date --iso-8601=seconds
```

## Rollback

- rollback realizado: NÃO;
- motivo: nenhuma alteração foi iniciada;
- rodada 06 continua instalada e segura, com painel desativado.

## Último passo concluído

Auditoria local e identificação do túnel remoto, com confirmação de ausência da rota/DNS
administrativa e preservação integral do servidor.

## Próximo passo exato

No ambiente Windows autenticado no Cloudflare:

1. abrir a aplicação Access `painelpix` e registrar uma evidência sanitizada dos atributos exigidos;
2. confirmar que a política protege o hostname inteiro, sem Everyone, Bypass, curinga ou exceção;
3. confirmar associação da regra à conta e identidade do proprietário;
4. obter privadamente `teamName` e AUD da própria aplicação, sem colocá-los em chat ou retorno;
5. disponibilizar uma sessão/API Cloudflare autorizada no Linux ou executar uma nova rodada assistida
   em que a rota remota seja criada com `Protect with Access` e validação JWT obrigatória;
6. somente depois retomar as Fases B–F da rodada 07.
