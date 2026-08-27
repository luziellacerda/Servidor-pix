# Retorno Linux — painel e sincronização — rodada 05

Data/hora: 2026-08-07T13:08:59-03:00  
Resultado final: `ROLLBACK_EXECUTADO`

## Resumo

O pacote exato da rodada 05 foi obtido por fast-forward, teve tamanho, hashes, manifesto interno e
autoteste aprovados. Foi criado backup root-only verificável e a versão 05 foi instalada
atomicamente.

Na validação funcional, a rota administrativa do hostname público atual respondeu e a tela de login
ficou acessível sem Cloudflare Access. Embora nenhuma senha administrativa estivesse configurada, isso
contrariou a regra de manter o painel somente local até existir hostname isolado e política Access.
O rollback preparado foi executado imediatamente.

A versão da rodada 04 foi restaurada, o painel público voltou a responder 404, o estado e o ambiente
privado permaneceram byte a byte idênticos ao backup e todos os serviços/endpoints ficaram saudáveis.

## Git e pacote

- commit após fast-forward: `6837917`;
- atualização Git: somente fast-forward;
- arquivos locais não rastreados preexistentes foram preservados;
- pacote:
  `outputs/TurboRamaPixOnlineServer-portable-RODADA05-20260807.zip`;
- tamanho: 88606 bytes;
- SHA-256 do pacote:
  `d5dfdc6e7057c5fe2dfb1fa4ac3f0e2f963d77b7a9c493c89e7265c186a569d4`;
- SHA-256 do DLL da rodada 05:
  `38ad5837d7ce7e17b5a5fc4149a026ca32c711a52226e358278d0ee8d903d327`;
- SHA-256 do DLL instalado ao final, após rollback:
  `cad619ed6c04520af4fa5c90afa31adafcc958f874495da039d8cb38af962ed9`;
- checksums internos: todos aprovados;
- autoteste temporário: aprovado;
- autoteste no diretório preparado: aprovado.

## Backup e rollback

- backup:
  `/var/backups/turborama-pix/round05-20260807T160546Z`;
- proprietário/permissão: `root:root`, modo `0700`;
- itens protegidos: aplicação, estado, ambiente privado, unidade systemd, configuração e unidade do
  cloudflared;
- manifesto `SHA256SUMS`: verificado integralmente, todos os itens `OK`;
- rollback executado: SIM;
- versão 05 isolada em:
  `/opt/turborama-pix.failed-round05-20260807T160546Z`;
- aplicação restaurada em: `/opt/turborama-pix`.

O diretório de chaves de proteção de dados criado automaticamente pela versão 05 durante o teste foi
movido, sem ser apagado nem exibido, para dentro do diretório da versão 05 isolada. O diretório de
estado voltou ao formato anterior.

## Integridade do estado

- hash do `state.json` antes e depois: idêntico;
- hash do `server.env` antes e depois: idêntico;
- conteúdo de `server.env`: nunca exibido;
- chaves privadas existentes: preservadas;
- migração persistida: NÃO;
- `PRECOS_EXISTENTES_PRESERVADOS: NAO_EXISTIAM`;
- licença existente: nenhuma conhecida no estado da rodada 04;
- máquinas cadastradas: nenhuma;
- nenhuma tabela ou preço foi criado, substituído, arredondado ou zerado.

## Painel administrativo

Estado observado temporariamente com a versão 05:

- rota local `/admin`: redirecionou para login;
- tela local de login: HTTP 200;
- cabeçalhos observados: `Cache-Control: no-store`, `X-Content-Type-Options: nosniff`,
  `Referrer-Policy: no-referrer` e Content Security Policy restritiva;
- login aceito: NÃO TESTADO, pois não existia senha administrativa;
- logout: NÃO TESTADO;
- cookie e token antifalsificação: não capturados nem exibidos;
- conta administrativa: não configurada;
- senha/hash: não criados;
- formulário Mercado Pago: não utilizado.

Estado final após rollback:

- painel local da rodada 05: NÃO INSTALADO/INATIVO;
- tela pública administrativa: HTTP 404;
- `PAINEL_PUBLICO: NAO`;
- hostname administrativo: NÃO CRIADO;
- `CLOUDFLARE_ACCESS: AGUARDANDO`;
- nginx e Cloudflare Tunnel: não alterados.

## Motivo técnico do rollback

O hostname existente `pix.lzgames.com.br` encaminha a aplicação inteira para a porta local do
TurboRama. Ao instalar a versão 05, isso também tornou `/admin/login` alcançável nesse hostname,
retornando HTTP 200 sem uma barreira Cloudflare Access. Não havia autorização/credencial disponível
para criar simultaneamente um hostname administrativo isolado e a política Access.

Publicar a versão 05 nessas condições violaria a separação exigida entre a API pública `/v1/*` e o
painel administrativo. Nenhuma regra existente foi substituída ou editada para contornar o problema.

## Estado de serviços e endpoints

| Item | Antes | Depois do rollback |
|---|---|---|
| `turborama-pix` | ativo | ativo e habilitado |
| `nginx` | ativo | ativo e habilitado |
| `cloudflared` | ativo | ativo e habilitado |
| `mariadb` | ativo | ativo e habilitado |
| health local PIX | saudável | saudável, `ready: true` |
| `https://pix.lzgames.com.br/v1/health` | saudável | saudável, `ready: true` |
| configuração local do túnel | válida | válida, `OK` |

## Portas protegidas

As portas `3302`, `3306`, `13306` e `23306` permaneceram inalteradas:

- nenhuma regra UFW, iptables, ip6tables, nftables, NAT ou roteamento foi alterada;
- nenhum bind do MariaDB foi alterado;
- nenhuma nova porta foi aberta;
- a porta 5187 continuou somente em loopback;
- nginx, aplicações LZGames e hostnames existentes não foram editados.

## Mercado Pago e cobrança

- `CREDENCIAL_MERCADOPAGO_ALTERADA: NAO`;
- Access Token solicitado: NÃO;
- Access Token exibido: NÃO;
- chamada real ao Mercado Pago: NÃO;
- `COBRANCA_CRIADA: NAO`;
- order/QR/pagamento criado: NÃO.

## Arquivos e recursos criados ou alterados

- clone privado atualizado por fast-forward até `6837917`;
- backup root-only criado em
  `/var/backups/turborama-pix/round05-20260807T160546Z`;
- versão 05 testada e depois isolada em
  `/opt/turborama-pix.failed-round05-20260807T160546Z`;
- versão 04 restaurada em `/opt/turborama-pix`;
- script criado:
  `ops/upgrade-turborama-pix-round05.sh`;
- retorno criado:
  `RETORNO-LINUX-RODADA-05.md`;
- nenhum commit ou push local foi realizado;
- firewall, nginx, Cloudflare, MariaDB, estado autenticado e ambiente privado: não alterados ao final.

## Comandos executados sem segredos

```bash
git status e git rev-parse
git pull --ff-only origin main
stat e sha256sum do pacote
unzip -l, unzip -t e extração temporária
sha256sum --check CHECKSUMS-SHA256.txt
dotnet TurboRamaPixOnlineServer.dll --self-test
sha256sum do DLL atual e do estado/ambiente
systemctl is-active/is-enabled dos serviços protegidos
curl dos endpoints de saúde
ss -lnt com filtro das portas protegidas
awk somente dos nomes das variáveis do ambiente privado
cloudflared ingress validate
leitura sanitizada de hostname/service do túnel
bash -n ops/upgrade-turborama-pix-round05.sh
execução root do script de backup/atualização
sha256sum --check do manifesto do backup
curl sanitizado das rotas administrativas, sem cookies
rollback atômico da aplicação
validação final de serviços, endpoints, hashes e painel público
```

Após iniciar a versão 05, a primeira consulta automática ocorreu antes da abertura da porta local e
falhou; uma repetição seguinte passou. Esse atraso transitório não motivou o rollback. O rollback foi
motivado exclusivamente pela exposição da tela administrativa sem Cloudflare Access.

## Último passo concluído

Rollback integral da aplicação para a rodada 04, com estado/ambiente preservados, painel público
novamente indisponível e todos os serviços e endpoints validados.

## Próximo passo exato no Windows

1. anexar e analisar este retorno;
2. ajustar a arquitetura/publicação para impedir que `/admin` seja servido pelo hostname público
   `pix.lzgames.com.br`, mantendo `/v1/*` inalterado;
3. definir previamente o hostname `painelpix.lzgames.com.br` e uma política Cloudflare Access
   restrita ao proprietário;
4. gerar nova rodada que permita instalar a aplicação somente quando a barreira Access puder ser
   aplicada e testada na mesma janela;
5. depois da publicação isolada, configurar a senha administrativa exclusivamente por entrada oculta
   no terminal local;
6. não cadastrar preços nem Mercado Pago antes da primeira sincronização autorizada do gabinete e do
   teste de segurança completo.
