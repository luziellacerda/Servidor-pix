# HANDOFF Linux — servidor como autoridade PIX — rodada 14

Data de origem: 15/08/2026
Branch obrigatória: `SERVIDOR-AUTORIDADE-PIX-20260815`

## Objetivo desta rodada

Publicar com segurança a versão em que o servidor LZ Games é a autoridade de
licença e de cada nova cobrança PIX. O computador do kiosk não guarda Access
Token nem Client Secret Mercado Pago. O EmulationStation e o agente continuam
funcionando localmente para jogos, créditos já concedidos e preços; somente a
criação de uma nova cobrança falha quando internet ou servidor estiverem
indisponíveis.

Este documento não contém senha, token, código de ativação, chave de estado nem
dados financeiros reais.

## Limites imutáveis

- Não alterar o site da empresa, seus arquivos, serviços, banco ou usuário.
- Não remover nem recriar o túnel Cloudflare `lz-fix`.
- Não ocupar nem alterar as portas 3302, 3306, 13306 ou 23306.
- O TurboRama deve escutar apenas em `127.0.0.1:5187`.
- Não abrir a porta 5187 no roteador ou firewall público.
- Não colocar segredo no Git, no retorno, no histórico do shell ou em logs.
- Não criar cobrança financeira durante a implantação. O teste financeiro real
  será uma etapa separada, acompanhada pelo proprietário.
- Diante de divergência real, parar e registrar a evidência; não inventar caminho,
  serviço, usuário, hostname ou valor.

## Estado esperado do código

- Cadastro de máquina por código de uso único.
- Prova RSA-PSS-SHA256 e sessão exclusiva.
- Modos `TPM_BOUND` e `SOFTWARE_BOUND_ONLINE`.
- Cadastro Mercado Pago por código bancário aleatório de 256 bits, uso único e
  validade de 15 minutos.
- Validação do Access Token e do par Loja/PDV antes de consumir o código.
- Access Token cifrado com AES-256-GCM somente no estado privado do servidor.
- Uma conexão Mercado Pago ativa por cliente.
- Cada criação e consulta de pagamento autorizada pelo servidor.
- Painel com login próprio, CSRF, confirmação de ações e auditoria.
- Os programas Windows de ativação/cadastro são portáteis do administrador e
  não ficam instalados no kiosk.

## 1. Auditoria e backup antes de mudar

Registrar no retorno, sem revelar conteúdo privado:

- commit e branch baixados;
- distribuição Linux e versão do .NET;
- status e nome real do serviço TurboRama existente;
- status do `cloudflared` e do conector do túnel `lz-fix`;
- caminhos reais do arquivo de configuração do cloudflared e da unit systemd;
- processo que escuta `127.0.0.1:5187`;
- resposta local e externa atual de `/v1/health`;
- resposta externa atual de `/admin` sem sessão autenticada;
- resultado de um teste do site da empresa antes da mudança.

Criar backup com data/hora, permissões e checksum de:

- `/opt/turborama-pix`;
- `/var/lib/turborama-pix`;
- `/etc/turborama-pix/server.env`;
- unit e overrides systemd do TurboRama;
- configuração real do cloudflared;
- configuração de proxy local relacionada ao TurboRama, se existir.

O backup das chaves e do estado precisa ficar cifrado e fora do Git. Provar uma
leitura/verificação do backup antes de continuar. Não copiar o banco de dados ou
arquivos do site da empresa para a pasta TurboRama.

## 2. Compilar e validar fora da instalação ativa

Usar o compilador versionado `COMPILAR-SERVIDOR-PIX-ONLINE.ps1` ou o artefato
portátil produzido pelo mesmo commit. A compilação deve estar em diretório novo,
nunca sobre `/opt/turborama-pix`.

Artefato Windows já compilado e revalidado depois de extraído:

- arquivo: `outputs/TurboRamaPixOnlineServer-portable-RODADA14-20260815.zip`;
- SHA-256 do ZIP: `07F3154D04CB64CA945315602D89B1AA2E03023C10A4536388C7B9EFF9CBFEA0`;
- fonte usada: commit `1dfdccf` da branch indicada no início;
- conteúdo: cinco arquivos, sem configuração privada ou segredo.

O Linux deve conferir primeiro o SHA-256 do ZIP e depois todos os itens de
`CHECKSUMS-SHA256.txt`. Se o commit, o hash ou o conteúdo divergirem, não
publicar e registrar a diferença no retorno.

Condições obrigatórias:

1. Git limpo e no commit informado.
2. Restore e build Release com warnings tratados como erro.
3. `TurboRamaPixOnlineServer.dll --self-test` retorna zero.
4. Publicação sem `.pdb`, fontes, scripts, chaves, tokens ou credenciais.
5. `CHECKSUMS-SHA256.txt` confere todos os arquivos publicados.
6. O binário publicado também executa `--self-test` com retorno zero.

O autoteste usa gateway financeiro falso. Resultado `SELF-TEST: OK` não prova
pagamento real e não pode ser descrito assim.

## 3. Estado privado e configuração

Preservar as chaves e o estado reais existentes. Nunca regenerar uma chave se
isso impedir a leitura do estado cifrado atual.

Confirmar:

- `/etc/turborama-pix/server.env` pertence a `root`, modo `600`;
- `/var/lib/turborama-pix` e o diretório de Data Protection pertencem somente ao
  usuário dedicado do serviço;
- as duas chaves de 32 bytes em Base64 são diferentes;
- `TURBORAMA_SERVER_STATE_FILE` aponta para o estado real preservado;
- `TURBORAMA_ADMIN_PASSWORD_HASH` contém apenas hash PBKDF2, nunca senha;
- `TURBORAMA_ADMIN_PUBLIC_HOST=painelpix.lzgames.com.br` somente depois de a
  proteção Cloudflare do painel estar comprovada;
- `ASPNETCORE_URLS=http://127.0.0.1:5187`;
- o serviço continua com usuário dedicado e hardening da unit de exemplo.

O Access Token Mercado Pago não pertence ao arquivo de ambiente. Ele entra uma
única vez por `POST /v1/enrollment/mercadopago`, usando o programa administrativo
e um código bancário temporário, e fica cifrado no estado privado.

## 4. Publicação atômica

1. Parar somente o serviço TurboRama.
2. Copiar a publicação validada para um diretório de versão novo.
3. Aplicar proprietário e permissões sem ampliar acesso ao estado.
4. Trocar a versão ativa de forma atômica ou manter um caminho de rollback
   igualmente verificável.
5. Recarregar systemd apenas se a unit mudou.
6. Iniciar somente o serviço TurboRama.
7. Não reiniciar nem recarregar o site, MariaDB ou serviços não relacionados.

Se o serviço não ficar ativo ou `/v1/health` local não retornar JSON 200,
executar rollback antes de qualquer mudança no Cloudflare.

## 5. Separar API de máquina e painel humano no Cloudflare

No teste externo real de 15/08/2026, a rota
`https://painelpix.lzgames.com.br/v1/health` retornava redirecionamento 302/HTML
para o Cloudflare Access. Isso impede o agente Windows de usar a API.

Usar o hostname já existente sem recriar o túnel:

- `painelpix.lzgames.com.br/admin*`: manter protegido pelo Cloudflare Access;
- `painelpix.lzgames.com.br/v1/*`: criar aplicação/política de caminho mais
  específica que não exija login humano;
- manter o encaminhamento do hostname para `http://127.0.0.1:5187` no túnel
  `lz-fix`, preservando todas as regras existentes e o catch-all final.

Antes de salvar, exportar ou registrar a configuração atual. Alterar somente a
regra desse hostname/caminho. A API não fica sem controle: ativação, sessão,
configuração, cadastro bancário e pagamento possuem prova/código no próprio
protocolo e rate limit; `/v1/health` é intencionalmente público.

Critérios externos obrigatórios, em janela privada e sem reutilizar cookies:

- `GET https://painelpix.lzgames.com.br/v1/health` = HTTP 200 e JSON; nunca 302,
  HTML ou tela de login;
- `GET https://painelpix.lzgames.com.br/admin` = barreira do Cloudflare Access
  antes do login TurboRama;
- depois do Access, `/admin` abre o login TurboRama e uma sessão válida abre o
  painel;
- acesso HTTP inseguro não entrega o painel e HTTPS envia HSTS;
- uma rota inexistente não revela stack trace;
- o site da empresa continua respondendo igual ao teste anterior.

Se o painel ficar público ou `/v1/health` continuar redirecionando, restaurar a
configuração Cloudflare anterior e registrar a falha. Não liberar gabinete.

## 6. Validação funcional sem movimentar dinheiro

Depois da publicação e da rota:

1. Confirmar readiness local e externa.
2. Confirmar que o painel lê licenças/máquinas existentes sem perder estado.
3. Confirmar que uma requisição protegida sem prova é recusada genericamente.
4. Confirmar que código de cadastro bancário não aparece em log depois de
   emitido e que expira/replay são recusados pelo autoteste.
5. Confirmar que o daemon não grava token em texto claro, resposta ou log.
6. Reiniciar apenas o serviço TurboRama e comprovar leitura do mesmo estado.
7. Confirmar novamente o site da empresa e o túnel.

Não gerar código bancário real nesta rodada se o administrador Windows ainda
não estiver pronto para consumi-lo dentro de 15 minutos.

## 7. Etapas humanas posteriores

Somente depois de todos os itens anteriores aprovados:

1. O proprietário cria/seleciona Cliente e Licença no painel.
2. O proprietário gera um código de ativação e ativa a máquina com o programa
   administrativo portátil.
3. O proprietário gera um código bancário e conclui o cadastro Mercado Pago com
   `CONFIGURAR-USER-TOKEN-PIX.exe`; o token não fica salvo no Windows.
4. O proprietário inicia uma cobrança de valor controlado no EmulationStation,
   paga e confirma manualmente no Mercado Pago.
5. Só então registrar que o teste financeiro real foi aprovado.

Credenciais usadas em conversas ou testes anteriores devem ser revogadas após
o teste controlado e substituídas por credenciais novas nunca publicadas.

## 8. Retorno obrigatório

Criar `RETORNO-LINUX-RODADA-14.md` sem segredos, contendo:

- commit implantado e checksums do pacote;
- backups criados e prova de leitura/restauração, sem conteúdo privado;
- status do serviço antes/depois;
- resultados e códigos de saída dos dois autotestes;
- respostas HTTP local/externa de `/v1/health` com tipo de conteúdo;
- prova de que `/admin` continua sob Access;
- prova de que o site existente continua intacto;
- arquivos/configurações efetivamente alterados;
- falhas observadas e se houve rollback;
- confirmação explícita de que nenhuma cobrança real foi criada nesta rodada.

Não incluir valores de `server.env`, cookies, JWT, chaves, senhas, token Mercado
Pago, código bancário ou código de ativação.
