# HANDOFF — painel Web e sincronização de preços — rodada 05

Data: 2026-08-07

## Instrução para a conversa no Linux

O usuário está no servidor Linux. Leia este arquivo inteiro antes de agir. Continue do estado da
rodada 04 e gere o retorno obrigatório. Não reinicie o projeto, não use comandos Windows e não peça
segredos no chat.

## Condição obrigatória antes de começar

Esta rodada só pode começar depois que o commit e o pacote abaixo estiverem no repositório privado:

- arquivo: `outputs/TurboRamaPixOnlineServer-portable-RODADA05-20260807.zip`;
- tamanho esperado: `88606` bytes;
- SHA-256 esperado:
  `d5dfdc6e7057c5fe2dfb1fa4ac3f0e2f963d77b7a9c493c89e7265c186a569d4`;
- SHA-256 esperado do DLL dentro do pacote:
  `38ad5837d7ce7e17b5a5fc4149a026ca32c711a52226e358278d0ee8d903d327`.

Se o pacote não existir no clone privado, o hash divergir ou o Git não puder fazer fast-forward, não
use outro arquivo e não altere o servidor. Gere `RETORNO-LINUX-RODADA-05.md` com resultado
`BLOQUEADO_POR_PACOTE`.

## Estado confirmado ao final da rodada 04

- Ubuntu 24.04.4 LTS, `x86_64`;
- ASP.NET Core Runtime 8.0.29;
- serviço `turborama-pix` ativo e habilitado;
- aplicação em `/opt/turborama-pix`;
- estado em `/var/lib/turborama-pix`;
- ambiente privado em `/etc/turborama-pix/server.env`, `root:root`, modo `0600`;
- API local em `127.0.0.1:5187` e API pública `https://pix.lzgames.com.br/v1/health` saudáveis;
- nginx, cloudflared, site e MariaDB saudáveis;
- a versão anterior permanece em `/opt/turborama-pix.rollback-20260807T133935Z`;
- backup anterior em `/var/backups/turborama-pix/round04-20260807T133935Z`;
- nenhuma licença, credencial Mercado Pago ou cobrança real foi criada na rodada 04.

## Proteções obrigatórias do sistema da empresa

É proibido alterar:

- portas ou regras `3302`, `3306`, `13306` e `23306`;
- UFW, iptables, ip6tables, nftables, NAT ou roteador;
- MariaDB, `bind-address`, bancos ou usuários existentes;
- site, nginx, hostnames existentes, regras existentes do Cloudflare Tunnel ou aplicações LZGames;
- API pública atual `pix.lzgames.com.br` e suas rotas `/v1/*`.

Não abrir a porta `5187` no firewall ou roteador. A aplicação deve continuar ouvindo somente em
`127.0.0.1:5187`.

## O que esta rodada adiciona

- painel administrativo em `/admin`;
- login com hash PBKDF2, cookie cifrado `Secure`/`HttpOnly`/`SameSite=Strict`, sessão de 30 minutos e
  proteção antifalsificação em toda alteração;
- lista de licenças, máquinas online/offline, tentativas recusadas e eventos;
- bloqueio ou ativação de novas cobranças PIX por licença;
- suspensão de licença/máquina, encerramento de sessão e permissão de edição por gabinete;
- troca do Access Token em campo oculto, validada contra o caixa antes de salvar e nunca exibida;
- edição dos cinco preços pelo site;
- leitura e escrita dos mesmos preços pelo EmulationStation com desafio e assinatura da máquina;
- controle otimista de versão: em conflito, a versão do servidor é preservada;
- migração automática que mantém exatamente os preços já existentes no estado;
- primeira máquina ativa autorizada a enviar os preços já existentes no EmulationStation;
- registro de máquinas desconhecidas e provas inválidas sem bloquear automaticamente a original;
- confiança em `X-Forwarded-Proto` somente quando enviado pelo proxy local do túnel.

O site e o EmulationStation não devem apontar para um arquivo físico compartilhado. A fonte de
verdade é o registro versionado dentro do estado autenticado do servidor; `owner-settings.json` é
somente o cache local protegido do gabinete.

## Regra absoluta sobre valores e Mercado Pago

- não substituir, zerar, arredondar nem inventar preços;
- se uma tabela já existir no estado, confirmar depois da atualização que os cinco valores foram
  preservados byte a byte/centavo a centavo;
- se ainda não existir licença/tabela no servidor, não cadastrar valores manualmente nesta rodada;
  a primeira sincronização do gabinete autorizado enviará os valores que já existem nele;
- não usar credencial que apareceu em conversa, captura ou Git;
- existe intenção de usar um Access Token novo somente no teste real posterior;
- deixar a troca futura disponível no formulário oculto do painel, mas não solicitar nem cadastrar
  Access Token nesta rodada;
- não criar order, QR, cobrança ou pagamento real.

## Fase A — auditoria e verificação sem alteração

1. Confirmar branch, commit, árvore limpa e fast-forward do clone privado.
2. Localizar somente o pacote exato da rodada 05.
3. Validar tamanho, SHA-256 externo, conteúdo do ZIP e `CHECKSUMS-SHA256.txt` interno.
4. Extrair em diretório temporário exclusivo.
5. Executar o autoteste sem dinheiro no diretório temporário e exigir código zero.
6. Registrar hash do DLL atual e capturar saúde de `turborama-pix`, nginx, cloudflared, MariaDB,
   endpoints local/público e portas protegidas antes da manutenção.
7. Ler, sem exibir segredos, os nomes das variáveis já presentes em `server.env` e as permissões.
8. Auditar de forma somente leitura a configuração atual do túnel. Não editar nesta fase.

## Fase B — backup e atualização reversível

1. Criar novo backup root-only com timestamp de `/opt/turborama-pix`, `/var/lib/turborama-pix`,
   `/etc/turborama-pix`, unidade systemd e configuração do cloudflared.
2. Gerar e verificar manifesto SHA-256 do backup antes de parar qualquer serviço.
3. Preparar a nova aplicação em diretório irmão e executar novamente checksums e autoteste.
4. Preparar rollback exato.
5. Parar somente `turborama-pix`.
6. Trocar somente a aplicação de forma atômica e preservar a versão anterior.
7. Não modificar o arquivo de estado nem as duas chaves existentes.
8. Iniciar somente `turborama-pix` e validar health local e público.
9. Confirmar nos logs que a migração, se necessária, preservou os preços existentes.

Se a nova versão não iniciar, o estado não abrir ou qualquer health falhar, restaurar imediatamente a
aplicação anterior. Não repetir sobre o estado; gerar retorno com `ROLLBACK_EXECUTADO`.

## Fase C — preparar login local sem expor senha

O painel deve usar somente a conta administrativa `admin` ou outro nome escolhido explicitamente pelo
proprietário.

1. Garantir que `/var/lib/turborama-pix/admin-data-protection` pertença exclusivamente ao usuário do
   serviço e tenha permissão restrita.
2. Acrescentar ao arquivo privado, preservando todas as linhas existentes:
   `TURBORAMA_ADMIN_USERNAME`, `TURBORAMA_ADMIN_PASSWORD_HASH` e
   `TURBORAMA_ADMIN_KEY_DIRECTORY`.
3. A senha deve ser digitada e confirmada pelo proprietário em entrada oculta de terminal local.
4. Gerar o hash com `--hash-admin-password`. Colocar somente o hash no ambiente privado.
5. Nunca mostrar ou registrar senha/hash completo em chat, retorno, histórico compartilhado ou Git.
6. Reiniciar somente `turborama-pix`.
7. Testar localmente o login enviando `X-Forwarded-Proto: https`, sem imprimir cookie ou token CSRF.
8. Confirmar status 200 da tela, login aceito, painel carregado e logout aceito.

Se não for possível obter a senha por entrada local realmente oculta, não improvise senha nem a peça
no chat. Deixe o painel não configurado e retorne `AGUARDANDO_SENHA_ADMIN_LOCAL`.

## Fase D — publicação administrativa isolada

Hostname recomendado: `painelpix.lzgames.com.br`.

1. Não tocar no hostname/API atual `pix.lzgames.com.br`.
2. Não alterar nginx: o novo hostname pode apontar pelo túnel para o mesmo
   `http://127.0.0.1:5187`, desde que a regra seja adicional.
3. Só adicionar o hostname se houver autorização/credencial Cloudflare já disponível e for possível
   validar a configuração antes de recarregar.
4. Fazer backup do arquivo real do túnel e preservar integralmente as regras existentes e o catch-all.
5. Inserir apenas a nova regra antes do catch-all; não criar outro túnel.
6. Proteger o hostname administrativo com Cloudflare Access e login permitido somente ao proprietário.
7. Não aplicar Cloudflare Access à API `pix.lzgames.com.br/v1/*` usada pelos gabinetes.
8. Validar o painel externamente, o login do aplicativo e a barreira do Cloudflare Access.
9. Revalidar imediatamente site, nginx, cloudflared, MariaDB, API e portas protegidas.

Se o hostname, a política Access ou a credencial de administração Cloudflare não estiverem
disponíveis, não exponha `/admin` publicamente e não modifique o túnel. Mantenha o painel local pronto
e retorne `PAINEL_LOCAL_PRONTO_AGUARDANDO_CLOUDFLARE_ACCESS`.

## Fase E — validação funcional sem valores novos e sem cobrança

1. Abrir o painel e confirmar que o Access Token nunca aparece.
2. Confirmar que o botão PIX altera somente autorização de cobrança e não altera preços.
3. Confirmar que licença/máquina/eventos são exibidos sem segredo.
4. Se já houver tabela, comparar os cinco valores de antes e depois; não salvar o formulário.
5. Se não houver licença, aceitar o painel vazio como estado correto e não criar cadastro nesta rodada.
6. Confirmar que a API `/v1/health` continua pública e saudável.
7. Confirmar que nenhuma order, cobrança, QR ou chamada real ao Mercado Pago ocorreu.

## Critérios de parada

Parar e preservar/restaurar o estado anterior se:

- pacote, hash ou checksums divergirem;
- autoteste, login ou health falhar;
- backup ou manifesto não puder ser verificado;
- algum preço existente mudar;
- estado/chaves não puderem ser abertos;
- qualquer serviço, site, túnel, banco ou porta protegida for afetado;
- a configuração Cloudflare exigir substituir regra existente;
- o painel só puder ser publicado sem Cloudflare Access;
- algum procedimento tentar imprimir senha, cookie, CSRF, Access Token ou chave;
- houver qualquer tentativa de criar cobrança real.

## Retorno obrigatório

Criar:

`/home/lz-servidor/turborama-download/Servidor-pix/RETORNO-LINUX-RODADA-05.md`

O retorno sanitizado deve informar:

- data/hora e resultado final;
- commit, tamanho/hash do pacote e hash do DLL instalado;
- backup, manifesto e caminho de rollback;
- se houve migração e confirmação `PRECOS_EXISTENTES_PRESERVADOS: SIM/NAO/NAO_EXISTIAM`;
- estado do painel local, login, logout e cabeçalhos de segurança, sem cookies/tokens;
- `PAINEL_PUBLICO: SIM/NAO` e hostname, se publicado;
- `CLOUDFLARE_ACCESS: ATIVO/AGUARDANDO`, sem credenciais;
- serviços/endpoints antes e depois;
- confirmação de que `3302`, `3306`, `13306` e `23306` permaneceram inalteradas;
- licenças e máquinas somente por identificadores não secretos;
- tabela de preços somente se já existia, em valores monetários, nunca credenciais;
- `CREDENCIAL_MERCADOPAGO_ALTERADA: NAO`;
- `COBRANCA_CRIADA: NAO`;
- arquivos criados/alterados e comandos executados sem valores secretos;
- rollback executado ou não;
- último passo concluído e próximo passo exato no Windows.

Nunca incluir senha, hash completo, cookie, token CSRF, Access Token, Client Secret, código de
ativação, chaves do servidor, conteúdo de `server.env`, credenciais Cloudflare, IP completo ou
cabeçalhos de autorização.
