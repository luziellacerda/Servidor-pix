# Retorno Linux — isolamento seguro do painel — rodada 06

Data/hora: 2026-08-07T14:43:40-03:00  
Resultado final: `INSTALADO_SEGURO_PAINEL_EXTERNO_DESATIVADO`

## Resumo

A rodada 06 foi obtida por fast-forward, validada e instalada atomicamente. O painel permanece
fechado por padrão porque `TURBORAMA_ADMIN_PUBLIC_HOST` está ausente. Os cinco caminhos
administrativos testados no hostname público da API retornaram 404, sem redirecionamento e sem corpo
de login. A API pública continuou respondendo 200.

Não havia Cloudflare Access administrativo disponível nesta janela. Nenhuma rota, hostname ou política
foi criada. A aplicação rodada 06 foi mantida instalada, conforme previsto pelo handoff.

## Git e pacote

- commit instalado: `102b8f0b6a436a999885188d0683a63d57755180`;
- commit-base `6837917`: ancestral confirmado;
- atualização do clone: somente fast-forward;
- pacote: `outputs/TurboRamaPixOnlineServer-portable-RODADA06-20260808.zip`;
- tamanho: 89509 bytes;
- SHA-256 do ZIP:
  `ff02ebe9472e86b62edd3bc4e4b31fe48627f56ab16a425f3837fc3c85ccf545`;
- SHA-256 do DLL instalado:
  `014182be32bfa540a0cde9edbc988519728c79249db9402e9ee6cbf3076f92ee`;
- ZIP estruturalmente válido;
- pacote contém somente manifesto, DLL e JSONs de runtime;
- nenhum fonte, PDB, script ou arquivo de segredo encontrado;
- checksums internos: todos `OK`;
- autoteste temporário e autoteste no diretório preparado: aprovados.

## Backup e rollback

- backup:
  `/var/backups/turborama-pix/round06-20260807T173720Z`;
- proprietário/permissão: `root:root`, modo `0700`;
- conteúdo: aplicação anterior, estado, ambiente privado, unidade systemd e configuração/unidade do
  cloudflared;
- manifesto `SHA256SUMS`: verificado integralmente, todos os itens `OK`;
- versão anterior preservada em:
  `/opt/turborama-pix.rollback-round06-20260807T173720Z`;
- rollback realizado: NÃO;
- versão 05 anteriormente isolada e backups anteriores: preservados.

## Estado e ambiente

- hash do `state.json` antes:
  `17f09a6335c7a31da5221d3f11aa3f16a144196fa3f500f25ceca98eca93ea65`;
- hash do `state.json` depois: idêntico;
- hash do `server.env` antes:
  `3cf2ff2338d7610d71aec2861cdf2ceef5df1941b6db5249f081d4461878fff0`;
- hash do `server.env` depois: idêntico;
- migração persistida no estado: NÃO;
- `PRECOS_EXISTENTES_PRESERVADOS: NAO_EXISTIAM`;
- `TURBORAMA_ADMIN_PUBLIC_HOST: VAZIO` — variável ausente, efeito equivalente a vazio;
- conteúdo do ambiente e chaves: não exibidos;
- `server.env`: `root:root`, modo `0600`;
- estado: `turborama-pix:turborama-pix`, modo `0700`;
- diretório Data Protection criado pela aplicação:
  `turborama-pix:turborama-pix`, modo `0700`.

## Teste obrigatório de isolamento público

Todos os testes foram feitos pela URL pública real, sem registrar cookies ou corpos:

| Caminho em `pix.lzgames.com.br` | Método | HTTP | Redirecionamento | Corpo de login |
|---|---|---:|---|---|
| `/admin` | GET | 404 | não | não |
| `/admin/` | GET | 404 | não | não |
| `/admin/login` | GET | 404 | não | não |
| `/admin/assets/admin.css` | GET | 404 | não | não |
| `/admin/actions/pix` | POST | 404 | não | não |
| `/v1/health` | GET | 200 | não | não aplicável |

O painel local também retorna 404 enquanto o hostname administrativo permanece vazio.

## Painel e Cloudflare

- `PAINEL_PUBLICO: NAO`;
- hostname reservado: `painelpix.lzgames.com.br`;
- hostname publicado: NÃO;
- `CLOUDFLARE_ACCESS: AGUARDANDO`;
- login TurboRama testado: NÃO, painel fechado;
- logout testado: NÃO;
- senha/hash administrativo criado: NÃO;
- cookie/CSRF capturado ou exibido: NÃO;
- configuração e regras existentes do Tunnel: não alteradas;
- configuração local do Tunnel: válida, resultado `OK`;
- API `pix.lzgames.com.br/v1/*`: não colocada atrás de Access.

## Serviços, site e portas

| Item | Antes | Depois |
|---|---|---|
| `turborama-pix` | ativo/habilitado | ativo/habilitado |
| `nginx` | ativo/habilitado | ativo/habilitado |
| `cloudflared` | ativo/habilitado | ativo/habilitado |
| `mariadb` | ativo/habilitado | ativo/habilitado |
| health local PIX | saudável | saudável, `ready: true` |
| health público PIX | saudável | saudável, `ready: true` |
| site principal | HTTP 200 | HTTP 200 |

As portas `3302`, `3306`, `13306` e `23306` permaneceram inalteradas. A porta 5187 continua
somente em loopback. Nenhuma regra de firewall, NAT, bind, nginx, MariaDB, site, aplicação LZGames,
hostname ou rota existente foi modificada.

## Dados comerciais

- clientes: 0;
- licenças: 0;
- máquinas: 0;
- preços criados/alterados: NÃO;
- `CREDENCIAL_MERCADOPAGO_ALTERADA: NAO`;
- Access Token solicitado ou usado: NÃO;
- cliente, ativação ou sessão criada: NÃO;
- `COBRANCA_CRIADA: NAO`;
- order, QR ou pagamento criado: NÃO.

## Arquivos e recursos criados ou alterados

- clone privado atualizado por fast-forward até `102b8f0`;
- aplicação rodada 06 instalada em `/opt/turborama-pix`;
- backup criado em
  `/var/backups/turborama-pix/round06-20260807T173720Z`;
- versão anterior preservada em
  `/opt/turborama-pix.rollback-round06-20260807T173720Z`;
- diretório privado Data Protection criado automaticamente sob o estado;
- script operacional criado:
  `ops/upgrade-turborama-pix-round06.sh`;
- retorno criado:
  `RETORNO-LINUX-RODADA-06.md`;
- nenhum commit ou push local;
- firewall, túnel, nginx, MariaDB, site, estado autenticado e ambiente privado não foram alterados.

## Comandos executados sem segredos

```bash
git status, rev-parse e merge-base --is-ancestor
git pull --ff-only origin main
stat, sha256sum, unzip -l, unzip -t e listagem sanitizada do ZIP
sha256sum --check CHECKSUMS-SHA256.txt
dotnet TurboRamaPixOnlineServer.dll --self-test
systemctl is-active/is-enabled dos serviços
curl dos endpoints, site e rotas administrativas
ss -lnt com filtro das portas protegidas
stat de permissões e awk somente dos nomes das variáveis privadas
cloudflared ingress validate e leitura sanitizada de hostname/service
bash -n ops/upgrade-turborama-pix-round06.sh
execução root do script de backup e atualização
sha256sum --check do manifesto do backup
comparação por SHA-256 do estado e ambiente antes/depois
journalctl sanitizado da inicialização e dos testes
```

Após iniciar a nova versão, a primeira consulta automática ocorreu antes da abertura da porta local e
falhou; uma repetição seguinte passou. Não houve falha persistente e não foi necessário rollback.

## Último passo concluído

Rodada 06 instalada e validada com painel falhando fechado por padrão, rotas administrativas invisíveis
no hostname da API e serviços/dados preservados.

## Próximo passo exato no Windows

1. anexar este retorno e confirmar o isolamento;
2. preparar a aplicação Cloudflare Access para todo o hostname
   `painelpix.lzgames.com.br`, permitindo somente a identidade do proprietário;
3. habilitar validação do token Access no conector/origem;
4. somente depois emitir nova rodada para adicionar a rota administrativa de forma aditiva;
5. com Access confirmado, gerar a senha administrativa em entrada oculta local, definir o hostname
   exato no ambiente privado e testar login/logout;
6. manter `pix.lzgames.com.br/admin*` retornando 404 e `/v1/*` público;
7. não criar preços, licença, máquina ou credencial financeira até rodada posterior autorizada.
