# HANDOFF — atualizar e configurar o TurboRama PIX no Linux — rodada 04

Data: 2026-08-07

## Instrução para a conversa no Linux

O usuário está no servidor Linux. Leia este arquivo completamente antes de agir. Continue do estado
descrito e gere o arquivo de retorno obrigatório ao terminar. Não reinicie o projeto e não use comandos
Windows.

## Condição obrigatória antes de começar

O pacote abaixo foi compilado e testado no Windows, mas sua publicação no repositório privado precisa
ser confirmada antes desta rodada:

- arquivo: `outputs/TurboRamaPixOnlineServer-portable-RODADA04-20260807.zip`;
- tamanho esperado: `59753` bytes;
- SHA-256 esperado:
  `49bade5ee22a29d01b254d16141972249f0cee4c6c33eaf9e06624c885f7d63c`.

Se o arquivo não estiver disponível no clone privado ou o hash não corresponder exatamente, não use
outro pacote e não altere o servidor. Gere o retorno com `BLOQUEADO_POR_PACOTE`.

## Estado já confirmado

- Ubuntu 24.04.4 LTS, `x86_64`;
- ASP.NET Core Runtime 8.0.29;
- serviço `turborama-pix` ativo e habilitado;
- aplicação atual em `/opt/turborama-pix`;
- estado em `/var/lib/turborama-pix`;
- ambiente privado em `/etc/turborama-pix/server.env`, modo `0600`;
- endpoint local `127.0.0.1:5187/v1/health` saudável;
- `https://pix.lzgames.com.br/v1/health` saudável;
- nginx, Cloudflare Tunnel, site e MariaDB funcionando;
- nenhuma credencial Mercado Pago ou licença de produção foi cadastrada;
- nenhuma cobrança real foi criada.

## Proteções obrigatórias do sistema da empresa

As portas `3302`, `3306` e `23306` foram declaradas pelo proprietário como pertencentes ao sistema da
empresa e ao roteador da Claro. A regra `13306` encontrada no servidor é diferente de `23306`, mas
também deve permanecer inalterada nesta rodada.

É proibido alterar:

- portas ou regras `3302`, `3306`, `13306` e `23306`;
- UFW, iptables, ip6tables, nftables, NAT ou roteador;
- MariaDB ou seu `bind-address`;
- nginx, site e Cloudflare Tunnel;
- qualquer aplicação LZGames existente.

## O que a atualização corrige

- impede que o buffer JSON da resposta do Mercado Pago seja apagado antes do processamento;
- valida o Access Token e o caixa com uma consulta segura antes de salvar a credencial;
- exige que o caixa encontrado pertença à credencial informada;
- adiciona `--validate-mercadopago CLIENTE` para repetir a validação sem criar cobrança;
- mantém criação de order dinâmica, idempotência e consulta de status existentes.

A validação usa `GET /pos?external_id=...`; ela não cria order, QR ou pagamento.

## Dados não secretos que precisam ser confirmados ao usuário

Antes de cadastrar produção, confirmar na conversa Linux:

1. preços exatos de 15, 30, 45, 60 e 120 minutos;
2. `external_id` alfanumérico do caixa/PDV criado no Mercado Pago;
3. confirmação de que existe um Access Token novo que nunca apareceu em chat ou captura.

Não pedir o Access Token no chat. O valor do caixa não é Public Key, Client ID, User ID nem o ID
numérico interno do PDV. Deve ser exatamente o `external_id` definido durante a criação do caixa, com
apenas letras e números e menos de 40 caracteres.

Identificadores planejados, salvo conflito:

- cliente: `CLI-TURBORAMA-TESTE`;
- licença: `TR-TURBORAMA-TESTE-001`;
- perfil: `SOFTWARE_BOUND_ONLINE`;
- máquinas permitidas: `1`.

Se preço, `external_id` ou confirmação da credencial nova não estiver disponível, é permitido atualizar
o binário, mas não cadastrar produção. Registrar `AGUARDANDO_DADOS` no retorno.

## Fase A — conferir pacote e saúde sem alterar

1. Atualizar somente o clone privado e localizar o pacote esperado.
2. Confirmar tamanho e SHA-256 externo.
3. Extrair para diretório temporário exclusivo.
4. Verificar `CHECKSUMS-SHA256.txt` dentro do pacote.
5. Executar `dotnet TurboRamaPixOnlineServer.dll --self-test` no diretório temporário.
6. Confirmar que o autoteste informa validação segura do caixa Mercado Pago e termina com código zero.
7. Revalidar serviços e endpoints antes da manutenção.

O autoteste usa respostas simuladas e não acessa o Mercado Pago nem cria cobrança.

## Fase B — backup e atualização reversível

Antes de parar o serviço:

1. criar backup root-only com timestamp de `/opt/turborama-pix`, `/var/lib/turborama-pix` e
   `/etc/turborama-pix`;
2. gerar manifesto SHA-256 e verificar todos os arquivos;
3. registrar proprietário e permissões sem exibir o conteúdo de `server.env`;
4. preparar comandos exatos de rollback;
5. manter nginx, cloudflared, MariaDB e os demais serviços ativos.

Depois:

1. preparar a nova aplicação em diretório irmão de `/opt/turborama-pix`;
2. validar novamente os checksums e o autoteste no diretório preparado;
3. parar somente `turborama-pix`;
4. trocar o diretório da aplicação de forma atômica, mantendo a versão anterior como rollback;
5. não remover a versão anterior nesta rodada;
6. não alterar o estado nem as chaves privadas durante a troca.

Se a nova versão não iniciar ou o health falhar, restaurar imediatamente o diretório anterior e gerar o
retorno com `ROLLBACK_EXECUTADO`.

## Fase C — administração local segura

Os comandos administrativos devem usar as variáveis já existentes em
`/etc/turborama-pix/server.env`, sem imprimir valores, e executar como o usuário `turborama-pix` para
preservar o proprietário do estado.

Se for criado um wrapper administrativo:

- caminho recomendado: `/usr/local/sbin/turborama-pix-admin`;
- proprietário: `root:root`;
- modo: `0700`;
- carregar o arquivo privado sem exibir seu conteúdo;
- transmitir segredos pelo ambiente, nunca por argumento;
- executar `/usr/bin/dotnet /opt/turborama-pix/TurboRamaPixOnlineServer.dll` como
  `turborama-pix`;
- nunca registrar Access Token em script, histórico, log ou arquivo temporário.

O serviço deve permanecer parado enquanto os comandos administrativos abrem o arquivo de estado.

## Fase D — cadastrar produção sem cobrança

1. listar licenças existentes;
2. se `TR-TURBORAMA-TESTE-001` já existir, conferir cliente, perfil e limite sem apagar ou recriar;
3. se não existir, criar para `CLI-TURBORAMA-TESTE`, `SOFTWARE_BOUND_ONLINE`, uma máquina;
4. não deixar o código inicial aparecer em saída capturada pelo Codex; descartá-lo com segurança e
   emitir um novo código somente ao final;
5. cadastrar os cinco preços confirmados em centavos;
6. executar `--set-mercadopago CLI-TURBORAMA-TESTE EXTERNAL_POS_ID`;
7. solicitar que o proprietário digite o Access Token novo diretamente na entrada oculta do terminal;
8. o comando deve confirmar que a conta e o caixa foram validados sem cobrança antes de gravar;
9. executar `--validate-mercadopago CLI-TURBORAMA-TESTE` e exigir sucesso;
10. emitir um novo código de ativação de uso único somente depois de todas as etapas anteriores;
11. o usuário deve copiar esse código privadamente para o gabinete Windows; nunca colocar no retorno,
    chat, Git ou captura;
12. iniciar `turborama-pix` e validar health local e público.

Não criar order, QR ou cobrança real nesta rodada. O teste real será iniciado posteriormente pelo único
gabinete Windows autorizado.

## Critérios de parada

Parar e preservar/recuperar o estado anterior se:

- pacote ou checksum não corresponder;
- autoteste falhar;
- backup ou verificação falhar;
- qualquer porta protegida ou serviço da empresa for afetado;
- site, nginx, Cloudflare, MariaDB ou endpoint PIX perder saúde;
- licença existente conflitar com outro cliente/perfil;
- preço ou caixa não for confirmado;
- Access Token for antigo/exposto;
- Mercado Pago não confirmar que o caixa pertence à credencial;
- qualquer comando tentar exibir ou registrar segredo.

## Retorno obrigatório

Criar:

`/home/lz-servidor/turborama-download/Servidor-pix/RETORNO-LINUX-RODADA-04.md`

O retorno sanitizado deve informar:

- data/hora e resultado final;
- hash do pacote e hash do DLL instalado;
- caminho do backup e verificação do manifesto;
- se atualização ocorreu ou se rollback foi executado;
- estado de `turborama-pix`, nginx, cloudflared, MariaDB e endpoints;
- confirmação de que as quatro portas protegidas permaneceram inalteradas;
- cliente, licença, perfil e limite de máquinas;
- cinco preços cadastrados;
- `external_id` do caixa, que é identificador não secreto;
- `CREDENCIAL_VALIDADA: SIM/NAO`, sem token;
- `CAIXA_VALIDADO: SIM/NAO`;
- `CODIGO_ATIVACAO_GERADO: SIM/NAO`, sem o código;
- confirmação `COBRANCA_CRIADA: NAO`;
- arquivos criados/alterados e comandos sem valores secretos;
- último passo concluído e próximo passo exato no Windows.

Nunca incluir Access Token, Client Secret, código de ativação, chaves do servidor, conteúdo de
`server.env`, credenciais Cloudflare, IP completo ou logs com cabeçalhos de autorização.

