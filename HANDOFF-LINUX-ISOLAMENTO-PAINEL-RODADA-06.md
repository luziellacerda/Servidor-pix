# HANDOFF — isolamento seguro do painel — rodada 06

Data: 2026-08-08

## Instrução para a conversa no Linux

O usuário está no servidor Linux. Leia este arquivo inteiro antes de agir. Continue exatamente do
retorno da rodada 05. Não reinicie o projeto, não use comandos Windows, não peça segredos no chat e
gere o retorno obrigatório ao terminar.

Esta rodada corrige o motivo do rollback anterior. A instalação da nova aplicação é permitida mesmo
quando o Cloudflare Access ainda não puder ser configurado, porque o painel agora falha fechado e
fica invisível no hostname público da API. A publicação do painel é uma fase opcional e separada.

## Pacote obrigatório

- arquivo: `outputs/TurboRamaPixOnlineServer-portable-RODADA06-20260808.zip`;
- tamanho esperado: `89509` bytes;
- SHA-256 esperado do ZIP:
  `ff02ebe9472e86b62edd3bc4e4b31fe48627f56ab16a425f3837fc3c85ccf545`;
- SHA-256 esperado do DLL dentro do pacote:
  `014182be32bfa540a0cde9edbc988519728c79249db9402e9ee6cbf3076f92ee`;
- commit base já confirmado no Linux: `6837917`;
- o commit da rodada 06 deve ser descendente de `6837917`, chegar somente por fast-forward e conter
  este handoff e o pacote exato.

Se o pacote não existir no clone privado, tamanho/hash/checksums divergirem, o Git não puder fazer
fast-forward ou o conteúdo tiver fonte, PDB, script ou segredo, não altere o servidor. Gere o retorno
com `BLOQUEADO_POR_PACOTE`.

## Estado confirmado após a rodada 05

- Ubuntu 24.04.4 LTS, `x86_64`;
- ASP.NET Core Runtime 8.0.29;
- a rodada 05 foi revertida por exposição da tela `/admin/login` no hostname da API;
- aplicação final restaurada para a rodada 04 em `/opt/turborama-pix`;
- SHA-256 do DLL final atual:
  `cad619ed6c04520af4fa5c90afa31adafcc958f874495da039d8cb38af962ed9`;
- serviço `turborama-pix` ativo/habilitado e escutando somente em `127.0.0.1:5187`;
- API local e `https://pix.lzgames.com.br/v1/health` saudáveis;
- estado e `server.env` restaurados byte a byte;
- nenhuma licença, máquina, tabela de preços, credencial Mercado Pago ou cobrança foi criada;
- versão 05 isolada em `/opt/turborama-pix.failed-round05-20260807T160546Z`;
- backup da rodada 05 em `/var/backups/turborama-pix/round05-20260807T160546Z`;
- nginx, cloudflared, site, MariaDB e configuração atual do túnel permanecem saudáveis.

## Troca de placa/TPM e transferência futura

Desconsiderar a troca de placa nesta rodada: não existe licença nem máquina cadastrada, portanto não
há identidade TPM antiga a migrar, revogar ou restaurar. Não criar cadastro somente para testar.

No uso futuro, o servidor guarda a chave **pública**, o `DeviceId`, o tipo de proteção e o estado da
máquina. A chave privada nunca deve ser copiada nem armazenada no servidor. Para trocar legitimamente
de computador, o painel revoga a máquina antiga, emite um novo código de ativação de uso único e a
máquina nova cria sua própria chave. Isso libera a licença sem enfraquecer a proteção contra clone.

## Correção incluída na rodada 06

- nova variável `TURBORAMA_ADMIN_PUBLIC_HOST`;
- vazia ou ausente: todas as rotas do painel ficam desativadas;
- `pix.lzgames.com.br/admin`, `/admin/login`, arquivos e ações administrativas retornam `404`;
- o painel só é aceito quando o hostname coincide exatamente com o nome administrativo autorizado;
- a requisição administrativa também precisa chegar como HTTPS pelo proxy loopback confiável;
- HTTP, prefixos, sufixos, curingas e nomes parecidos retornam `404`;
- `/v1/*` continua funcionando no hostname atual da API;
- nenhuma mudança no protocolo do gabinete, preços, Mercado Pago ou estado financeiro.

O aplicativo confia em `X-Forwarded-Proto` somente quando a conexão vem do proxy loopback já
configurado. Não ampliar a lista de proxies e não aceitar cabeçalhos encaminhados de qualquer origem.

## Proteções absolutas do sistema da empresa

É proibido alterar:

- portas/regras `3302`, `3306`, `13306` e `23306`;
- UFW, iptables, ip6tables, nftables, NAT ou roteador;
- MariaDB, `bind-address`, bancos ou usuários;
- site, arquivos do site, nginx, aplicações LZGames ou hostnames existentes;
- regra atual da API `pix.lzgames.com.br` e rotas `/v1/*`;
- estado, chaves e credenciais privadas fora do procedimento de backup/atualização descrito aqui.

Não abrir `5187` no firewall ou roteador. Não criar outro túnel. Não apagar backups nem a versão 05
isolada. Toda alteração opcional no Cloudflare precisa ser apenas aditiva e reversível.

## Fase A — auditoria somente leitura

1. Confirmar branch, commit, árvore local e fast-forward do clone privado.
2. Localizar somente o ZIP exato da rodada 06.
3. Validar tamanho, SHA-256 externo, teste do ZIP e todos os checksums internos.
4. Extrair em diretório temporário exclusivo e executar `--self-test`; exigir código zero.
5. Registrar o hash do DLL instalado e confirmar que corresponde ao estado final da rodada 05.
6. Capturar, antes de qualquer mudança, saúde/estado de TurboRama, nginx, cloudflared, MariaDB, site,
   endpoints local/público, túnel e portas protegidas.
7. Confirmar somente os **nomes** das variáveis de `server.env`, sem exibir valores.
8. Confirmar que `TURBORAMA_ADMIN_PUBLIC_HOST` ainda está ausente ou vazio.
9. Auditar a configuração atual do túnel somente em leitura.

Qualquer divergência no estado confirmado exige parada sem alteração e retorno sanitizado.

## Fase B — backup e atualização atômica

1. Criar backup root-only novo, com timestamp, de aplicação, estado, ambiente privado, unidade
   systemd e configuração/unidade cloudflared.
2. Gerar manifesto SHA-256 e verificá-lo antes de parar serviço.
3. Preparar a rodada 06 em diretório irmão, validar checksums e repetir o autoteste.
4. Preparar rollback exato para a rodada 04 atual.
5. Manter `TURBORAMA_ADMIN_PUBLIC_HOST` ausente ou vazio.
6. Parar somente `turborama-pix`, promover somente a aplicação de forma atômica e preservar a versão
   anterior.
7. Não alterar as duas chaves existentes nem cadastrar dados de negócio.
8. Iniciar somente `turborama-pix` e aguardar readiness.
9. Validar saúde local e pública da API.
10. Confirmar obrigatoriamente, pela URL pública real, que todos estes caminhos no hostname da API
    respondem `404`, sem redirecionamento e sem corpo de login:
    `/admin`, `/admin/`, `/admin/login`, `/admin/assets/admin.css` e uma ação administrativa.
11. Confirmar que `/v1/health` continua `200` e que as rotas `/v1/*` não foram colocadas atrás de
    Cloudflare Access.

Se a aplicação não iniciar, o estado não abrir, API/serviços falharem ou qualquer `/admin` aparecer
no hostname da API, restaurar imediatamente a rodada 04. Não repetir sobre o estado. Resultado:
`ROLLBACK_APLICACAO_EXECUTADO`.

## Fase C — painel totalmente fechado por padrão

Sem credenciais administrativas e sem hostname público configurado, é correto que:

- o painel externo permaneça desativado;
- o painel local também responda `404` enquanto o hostname administrativo estiver vazio;
- `pix.lzgames.com.br/admin*` responda `404`;
- nenhum cookie, token CSRF, hash ou segredo seja impresso.

Se não houver acesso administrativo ao Cloudflare nesta janela, pare aqui **sem rollback da
aplicação**. A correção já protege a API. Mantenha o túnel e `server.env` inalterados e use o resultado
`INSTALADO_SEGURO_PAINEL_EXTERNO_DESATIVADO`.

## Fase D — publicação opcional do painel, somente com Cloudflare Access pronto

Hostname reservado: `painelpix.lzgames.com.br`.

Executar esta fase somente se a conta Cloudflare autorizada estiver disponível e todas as ações
puderem ser verificadas. A ordem é obrigatória:

1. Criar primeiro uma aplicação Cloudflare Access que proteja **todo**
   `painelpix.lzgames.com.br`, permitindo apenas a identidade do proprietário.
2. Não criar política `Bypass`, `Everyone`, acesso público ou exceção de caminho.
3. Habilitar no conector/túnel a validação do token Access para impedir acesso à origem por erro de
   configuração.
4. Confirmar que a aplicação Access existe antes de publicar a rota do túnel.
5. Fazer backup verificável da configuração real do cloudflared.
6. Acrescentar somente uma nova regra para `painelpix.lzgames.com.br` apontando ao mesmo
   `http://127.0.0.1:5187`, antes do catch-all. Preservar integralmente todas as regras atuais.
7. Validar a configuração do túnel antes de recarregar somente `cloudflared`.
8. Com `TURBORAMA_ADMIN_PUBLIC_HOST` ainda vazio, confirmar que o novo hostname chega ao aplicativo
   mas o aplicativo responde `404` depois da autenticação Access.
9. Gerar a senha administrativa somente por entrada local oculta e confirmada. Salvar no ambiente
   privado apenas usuário, hash PBKDF2 e diretório de Data Protection; nunca a senha.
10. Definir `TURBORAMA_ADMIN_PUBLIC_HOST=painelpix.lzgames.com.br` no arquivo privado, sem URL, porta,
    caminho ou curinga.
11. Reiniciar somente `turborama-pix`.
12. Confirmar barreira Cloudflare Access, depois login TurboRama, painel e logout, sem registrar
    cookie, CSRF, senha ou hash.
13. Reconfirmar que o mesmo `/admin*` continua `404` em `pix.lzgames.com.br`.
14. Revalidar site, API, nginx, cloudflared, MariaDB, túnel e portas protegidas.

Se qualquer etapa do Access/túnel falhar, desfazer **somente** a rota administrativa adicional e as
novas variáveis do painel, usando o backup; manter a aplicação rodada 06 instalada com hostname
administrativo vazio. Não restaurar a rodada 04 se API e aplicação continuarem saudáveis. Resultado:
`INSTALADO_SEGURO_PAINEL_EXTERNO_AGUARDANDO_ACCESS`.

## Fase E — validação sem cadastro e sem dinheiro

1. Não criar licença, cliente, máquina, código de ativação ou sessão comercial.
2. Não cadastrar nem alterar preços; o EmulationStation enviará os valores existentes apenas em
   rodada posterior autorizada.
3. Não solicitar, colar, validar ou gravar Access Token Mercado Pago.
4. Não criar order, QR, cobrança ou pagamento.
5. Confirmar que o painel vazio é o estado correto.
6. Confirmar que nenhum segredo apareceu em logs, retorno, Git ou histórico.

## Critérios de parada

Parar e preservar/restaurar conforme a fase se:

- pacote, hash, manifesto, autoteste ou fast-forward divergirem;
- backup/rollback não puder ser provado;
- estado ou chaves não abrirem;
- API, site, nginx, cloudflared, MariaDB ou porta protegida forem afetados;
- `/admin*` não retornar `404` no hostname da API;
- a publicação administrativa exigir substituir uma regra existente;
- Access não proteger o hostname inteiro antes da rota ser publicada;
- houver tentativa de imprimir ou transportar senha, hash, cookie, CSRF, Access Token, Client Secret,
  chave do servidor, código de ativação ou conteúdo de `server.env`;
- algum passo tentar criar cadastro comercial, alterar preço ou cobrar dinheiro.

## Rollback obrigatório

- falha da aplicação/API/estado: restaurar atomicamente a rodada 04 e o backup correspondente;
- falha somente na publicação administrativa: restaurar apenas configuração adicional do painel e
  do túnel, manter a rodada 06 com `TURBORAMA_ADMIN_PUBLIC_HOST` vazio;
- depois de qualquer rollback, revalidar integralmente serviços, endpoints, estado e portas;
- não apagar o material que falhou: isolar com timestamp e permissão root-only para auditoria.

## Retorno obrigatório

Criar:

`/home/lz-servidor/turborama-download/Servidor-pix/RETORNO-LINUX-RODADA-06.md`

O retorno sanitizado deve informar:

- data/hora e um destes resultados finais:
  `INSTALADO_SEGURO_PAINEL_EXTERNO_DESATIVADO`,
  `INSTALADO_COM_PAINEL_ISOLADO_E_ACCESS`,
  `INSTALADO_SEGURO_PAINEL_EXTERNO_AGUARDANDO_ACCESS`,
  `ROLLBACK_APLICACAO_EXECUTADO` ou `BLOQUEADO_POR_PACOTE`;
- commit, tamanho/hash do ZIP e hash do DLL instalado;
- backup, manifesto, versão anterior e caminho de rollback;
- hashes sanitizados de estado/ambiente antes/depois e migração ocorrida;
- `PRECOS_EXISTENTES_PRESERVADOS: NAO_EXISTIAM` esperado;
- códigos HTTP de `/v1/health` e dos cinco caminhos `/admin*` no hostname da API;
- `TURBORAMA_ADMIN_PUBLIC_HOST: VAZIO/CONFIGURADO`, sem outros valores de ambiente;
- `PAINEL_PUBLICO: SIM/NAO`, hostname e `CLOUDFLARE_ACCESS: ATIVO/AGUARDANDO`;
- login/logout testados ou não, sem cookies/tokens/hash;
- serviços/endpoints/portas protegidas antes e depois;
- confirmação de que site, nginx, MariaDB e regras existentes do túnel não foram modificados;
- quantidade de clientes/licenças/máquinas, esperada `0/0/0`;
- `CREDENCIAL_MERCADOPAGO_ALTERADA: NAO`;
- `COBRANCA_CRIADA: NAO`;
- arquivos criados/alterados, comandos executados sem valores secretos e rollback realizado;
- último passo concluído e próximo passo exato no Windows.

Nunca incluir senha, hash completo, cookie, token CSRF, Access Token, Client Secret, código de
ativação, chave privada/pública completa, chaves do servidor, conteúdo de `server.env`, credencial
Cloudflare, IP completo ou cabeçalho de autorização.

## Referências de segurança usadas no desenho

- Microsoft: aceitar cabeçalhos encaminhados apenas de proxies explicitamente confiáveis;
- Cloudflare: criar a aplicação Access antes de publicar a rota do túnel;
- Cloudflare: validar o token Access no conector/origem e proteger o hostname inteiro.
