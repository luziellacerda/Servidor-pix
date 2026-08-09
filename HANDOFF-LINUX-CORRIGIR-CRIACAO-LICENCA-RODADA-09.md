# HANDOFF — corrigir criação de licença no painel — rodada 09

Data: 2026-08-09

## Instrução para a conversa no Linux

O usuário está no servidor Linux. Leia este arquivo inteiro antes de agir e continue exatamente do
estado confirmado em `RETORNO-LINUX-RODADA-08.md`. Não reinicie o projeto, não peça segredos no chat
e gere o retorno sanitizado obrigatório ao terminar.

Esta rodada troca somente os binários do serviço `turborama-pix`. É proibido modificar o site, o
Cloudflare Tunnel, o Cloudflare Access, nginx, MariaDB, firewall, roteador, preços, Mercado Pago,
licenças ou máquinas de produção durante a instalação.

## Problema comprovado no Windows

No painel autenticado, o formulário “Criar licença” estava preenchido e válido, mas o botão “Criar e
mostrar código único” podia permanecer na mesma página sem confirmação e sem criar a licença.

A rodada 09 adiciona:

- botão explicitamente definido como `type=submit`;
- envio assistido do formulário pelo navegador depois da validação nativa;
- bloqueio temporário contra clique duplicado;
- texto visível “Criando licença...” durante o envio;
- aviso após dez segundos quando o servidor não confirma a navegação;
- arquivo próprio `/admin/assets/admin.js`, permitido pela política CSP somente no hostname
  administrativo já isolado;
- autoteste de regressão do caminho de envio.

O endpoint protegido que efetivamente cria a licença não foi afrouxado: continua exigindo sessão
administrativa, antifalsificação e confirmação da senha administrativa.

## Validação já concluída no Windows

- compilação Release: zero erros e zero avisos;
- autoteste completo: aprovado;
- teste HTTP isolado, com estado descartável e sem dados de produção:
  - login GET: `200`;
  - login POST: `302`;
  - painel autenticado: `200`;
  - novo JavaScript administrativo: `200`;
  - POST de criação de licença descartável: `200`;
  - página de código único exibida: SIM;
  - licença descartável apareceu na lista: SIM;
- arquivos temporários, cookies, código descartável e chaves de teste foram removidos;
- nenhuma licença, preço, credencial ou cobrança foi criada no servidor real.

## Pacote obrigatório

- arquivo: `outputs/TurboRamaPixOnlineServer-portable-RODADA09-20260809.zip`;
- tamanho esperado: `90815` bytes;
- SHA-256 esperado do ZIP:
  `633306889e0781ad3338474b587f919f56c9603f6532503d4bd9554f7a5147a2`;
- SHA-256 esperado do DLL dentro do pacote:
  `4c49b67a7ae719def39554b1064d71d0239f9b9bf5eb1c96bcff95b3644749a2`;
- commit instalado atualmente: `102b8f0b6a436a999885188d0683a63d57755180`;
- o commit que contém a rodada 09 deve ser descendente do commit acima, chegar somente por
  fast-forward e conter este handoff, as duas alterações de fonte e o pacote exato.

O ZIP deve conter somente:

- `CHECKSUMS-SHA256.txt`;
- `TurboRamaPixOnlineServer.deps.json`;
- `TurboRamaPixOnlineServer.dll`;
- `TurboRamaPixOnlineServer.runtimeconfig.json`;
- `TurboRamaPixOnlineServer.staticwebassets.endpoints.json`.

Se Git, pacote, tamanho, hash, conteúdo, checksums internos ou autoteste divergirem, não altere o
servidor. Gere o retorno com resultado `BLOQUEADO_POR_PACOTE`.

## Estado confirmado antes desta rodada

- painel externo: `https://painelpix.lzgames.com.br/admin`;
- Cloudflare Access ativo e comprovado;
- login interno TurboRama ativo e comprovado;
- serviço `turborama-pix` ativo/habilitado em `127.0.0.1:5187`;
- API pública `https://pix.lzgames.com.br/v1/health` saudável;
- aplicação em `/opt/turborama-pix`;
- estado em `/var/lib/turborama-pix`;
- ambiente privado em `/etc/turborama-pix/server.env`, `root:root`, modo `0600`;
- dados comerciais antes da tentativa: `0` clientes, `0` licenças, `0` máquinas, `0` pagamentos,
  `0` credenciais Mercado Pago e `0` tabelas de preços;
- a tentativa no painel não criou licença;
- somente a trilha de auditoria pode ter crescido por login/logout;
- backup confirmado da rodada 08:
  `/var/backups/turborama-pix/round08-20260808T113741Z`.

## Proteções absolutas do sistema da empresa

É proibido alterar:

- portas ou regras `3302`, `3306`, `13306` e `23306`;
- UFW, iptables, ip6tables, nftables, NAT ou roteador;
- MariaDB, bancos, usuários ou `bind-address`;
- site, arquivos do site, nginx ou hostnames existentes;
- conector, túnel, rotas, DNS ou políticas Cloudflare existentes;
- `pix.lzgames.com.br`, `painelpix.lzgames.com.br` ou suas regras;
- valores de `server.env`, chaves, senha/hash administrativo e credenciais;
- estado comercial/financeiro de produção.

Não abrir a porta `5187`. Não criar outro túnel. Não reconfigurar Cloudflare Access. Não apagar
backups nem versões anteriores.

## Fase A — auditoria somente leitura

1. Ler `RETORNO-LINUX-RODADA-08.md` por completo.
2. Confirmar branch, commit atual, árvore e possibilidade de atualização somente fast-forward.
3. Confirmar que o commit recebido é descendente de
   `102b8f0b6a436a999885188d0683a63d57755180`.
4. Localizar somente o ZIP exato da rodada 09.
5. Validar tamanho, SHA-256 externo, lista dos cinco arquivos e todos os checksums internos.
6. Extrair em diretório temporário novo e executar `--self-test`; exigir código zero.
7. Registrar o hash do DLL instalado antes da troca; o valor esperado da rodada 06 é
   `014182be32bfa540a0cde9edbc988519728c79249db9402e9ee6cbf3076f92ee`.
8. Capturar antes da mudança: estados de `turborama-pix`, nginx, cloudflared e MariaDB; health local
   e público; site; painel; listeners e portas protegidas.
9. Registrar hashes de aplicação, estado, unidade, ambiente e configuração Cloudflare sem exibir
   conteúdo nem valores secretos.
10. Ler o estado de forma sanitizada e registrar contagens de clientes, licenças, máquinas,
    pagamentos, credenciais e tabelas. A expectativa é `0/0/0/0/0/0`.

Qualquer divergência relevante exige parada sem alteração e retorno sanitizado.

## Fase B — teste funcional isolado, sem produção

Antes da instalação, usar exclusivamente um diretório temporário novo, chaves aleatórias novas,
estado descartável, porta local livre e senha temporária que nunca seja mostrada ou registrada.

1. Iniciar o binário extraído sem apontar para `/var/lib/turborama-pix` ou
   `/etc/turborama-pix/server.env`.
2. Validar login, carregamento de `/admin/assets/admin.js` e submissão HTTP real do formulário.
3. Criar uma única licença fictícia somente no estado temporário.
4. Exigir resposta `200`, página de código único e presença da licença fictícia na lista.
5. Não registrar nem transportar o código único descartável.
6. Encerrar o processo temporário e remover com segurança todo o diretório descartável.

Se esse teste falhar, não instalar. Resultado: `BLOQUEADO_POR_TESTE_FUNCIONAL`.

## Fase C — backup e atualização atômica

1. Criar backup root-only novo, com timestamp, de `/opt/turborama-pix`,
   `/var/lib/turborama-pix`, `/etc/turborama-pix`, unidade systemd e configuração/unidade
   cloudflared.
2. Gerar e verificar manifesto SHA-256 antes de parar qualquer serviço.
3. Preparar a rodada 09 em diretório irmão, validar checksums e repetir `--self-test`.
4. Preparar rollback exato para a aplicação anterior.
5. Parar somente `turborama-pix`.
6. Promover somente os cinco arquivos da aplicação de forma atômica, mantendo proprietários e
   permissões já corretos.
7. Não copiar `server.env`, estado, chaves, Data Protection ou configuração Cloudflare para a nova
   pasta de aplicação.
8. Iniciar somente `turborama-pix` e aguardar readiness.
9. Exigir o novo SHA-256 do DLL instalado.
10. Confirmar health local e público.

Se o serviço, estado ou health falhar, restaurar imediatamente a aplicação anterior e reiniciar
somente `turborama-pix`. Resultado: `ROLLBACK_APLICACAO_EXECUTADO`.

## Fase D — validação sem criar dados de produção

1. Confirmar que `pix.lzgames.com.br/admin*` continua retornando `404` e que `/v1/health` continua
   `200`.
2. Confirmar que o hostname administrativo continua protegido pelo Cloudflare Access.
3. Autenticar no painel sem registrar e-mail, senha, código, cookie, JWT ou token antifalsificação.
4. Confirmar que o painel continua vazio e que o novo arquivo `/admin/assets/admin.js` retorna `200`
   somente no hostname administrativo autorizado.
5. Confirmar no HTML/DOM:
   - botão com tipo de envio explícito;
   - script administrativo carregado;
   - área de mensagem acessível presente.
6. Não apertar “Criar e mostrar código único” nesta fase. A criação real será feita pelo
   proprietário após o retorno da rodada.
7. Revalidar site, API, nginx, cloudflared, MariaDB, listeners e portas protegidas.
8. Comparar estado antes/depois estruturalmente. Aceitar somente novas entradas de auditoria causadas
   por login/logout; clientes, licenças, máquinas, pagamentos, credenciais e preços devem permanecer
   idênticos.

## Critérios de parada

Parar ou executar rollback conforme a fase se:

- houver divergência de Git, pacote, hash, conteúdo ou checksums;
- autoteste ou teste HTTP isolado falhar;
- backup ou manifesto não puder ser comprovado;
- estado ou chaves não abrirem;
- algum dado comercial/financeiro mudar;
- API, painel, site, nginx, cloudflared, MariaDB, listener ou porta protegida for afetado;
- `/admin*` deixar de retornar `404` no hostname público da API;
- algum passo tentar alterar Cloudflare, firewall, banco, site ou credencial;
- algum segredo, cookie, código de ativação ou conteúdo de ambiente aparecer no retorno.

## Rollback obrigatório

- falha de aplicação/API/estado: restaurar atomicamente a aplicação anterior;
- preservar o estado e o ambiente originais, salvo restauração comprovadamente necessária por
  corrupção causada nesta rodada;
- reiniciar somente `turborama-pix`;
- revalidar todos os serviços, endpoints e portas;
- isolar a versão que falhou com timestamp e permissão root-only; não apagar evidências.

## Retorno obrigatório

Criar:

`/home/lz-servidor/turborama-download/Servidor-pix/RETORNO-LINUX-RODADA-09.md`

O retorno deve informar, sem segredos:

- data/hora e resultado final:
  `ATUALIZADO_E_VALIDADO`, `BLOQUEADO_POR_PACOTE`, `BLOQUEADO_POR_TESTE_FUNCIONAL` ou
  `ROLLBACK_APLICACAO_EXECUTADO`;
- commit instalado, tamanho/hash do ZIP e hash do DLL antes/depois;
- caminho do backup, manifesto, versão anterior e rollback preparado/executado;
- resultado do autoteste e do teste HTTP isolado, sem código único ou senha;
- códigos HTTP de health local/público, site, painel e isolamento `/admin*` na API;
- estado de `turborama-pix`, nginx, cloudflared e MariaDB antes/depois;
- confirmação de que Cloudflare, nginx, site, banco, firewall, roteador e portas `3302`, `3306`,
  `13306` e `23306` permaneceram inalterados;
- contagens comerciais antes/depois;
- confirmação de que somente auditoria pode ter mudado no estado;
- `LICENCA_PRODUCAO_CRIADA: NAO`;
- `PRECOS_ALTERADOS: NAO`;
- `CREDENCIAL_MERCADOPAGO_ALTERADA: NAO`;
- `COBRANCA_CRIADA: NAO`;
- arquivos alterados/criados, sem conteúdo sensível;
- último passo concluído e próximo passo exato: o proprietário testar uma única criação real no
  painel, depois guardar o código diretamente no gabinete autorizado.

Nunca incluir senha, hash completo, cookie, token antifalsificação, JWT, código de e-mail, código de
ativação, Access Token, Client Secret, chaves do servidor, conteúdo de `server.env`, credencial
Cloudflare, IP completo ou cabeçalho de autorização.
