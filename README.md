# Servidor PIX on-line do TurboRama

Backend privado de licenciamento, prova de máquina e criação de cobranças PIX do TurboRama. O servidor mantém as credenciais financeiras fora do computador do consumidor e exige autorização on-line para cada nova cobrança.

> Este repositório contém código comercial sensível e deve permanecer **privado**. Nunca coloque Access Token, Client Secret, chaves de cifragem, arquivo de estado ou dados de clientes no Git.

## O que está incluído

- API ASP.NET Core em `.NET 8`;
- ativação por código de uso único;
- prova de posse da chave privada com RSA-PSS-SHA256;
- perfis `TPM_BOUND` e `SOFTWARE_BOUND_ONLINE`;
- sessão exclusiva e registro de tentativas de clonagem;
- tabela de preços versionada e sincronizada entre painel e EmulationStation;
- painel Web administrativo com login, proteção CSRF, sessão curta e trilha de auditoria;
- bloqueio remoto de novas cobranças PIX, licenças e máquinas;
- transferência administrativa de licença para outra placa-mãe ou nova identidade, com código único;
- Access Token Mercado Pago cifrado com AES-256-GCM;
- validação sem cobrança de que o caixa informado pertence à credencial Mercado Pago;
- criação e consulta de orders QR do Mercado Pago;
- estado autenticado com HMAC-SHA256 e gravação atômica;
- autoteste sem dinheiro real e sem conexão com o Mercado Pago.

`USB_TOKEN_BOUND` está reservado, mas permanece bloqueado até a escolha e validação de um token criptográfico real. Pendrive comum não é aceito como proteção.

## Estrutura

```text
src/TurboRamaPixOnlineServer/   código do servidor e protocolo compartilhado
docs/                           arquitetura e operação
deploy/linux/                   exemplos de serviço; não altera o servidor automaticamente
COMPILAR-SERVIDOR-PIX-ONLINE.ps1
```

## Teste local

Pré-requisitos: SDK do .NET 8 ou superior compatível e PowerShell.

```powershell
dotnet build .\src\TurboRamaPixOnlineServer\TurboRamaPixOnlineServer.csproj -c Release -warnaserror
dotnet .\src\TurboRamaPixOnlineServer\bin\Release\net8.0\TurboRamaPixOnlineServer.dll --self-test
```

O autoteste usa um provedor falso e não cria cobranças reais.

## Compilação para o Linux

O pacote portátil é o recomendado para o servidor Linux com ASP.NET Core Runtime 8 instalado. Com o
Git limpo e revisado:

```powershell
.\COMPILAR-SERVIDOR-PIX-ONLINE.ps1 -RuntimeIdentifier portable
```

A saída fica em `outputs/servidor-pix-online-portable`, sem iniciador específico do Windows, e inclui
checksums SHA-256. A pasta `outputs` é ignorada pelo Git. O alvo `linux-x64` permanece disponível
quando for necessário publicar para um runtime específico.

## Configuração secreta

O processo exige estas variáveis, configuradas somente no cofre ou no arquivo privado do servidor:

```text
TURBORAMA_SERVER_STATE_FILE
TURBORAMA_SERVER_STATE_KEY
TURBORAMA_SERVER_SECRET_KEY
TURBORAMA_PAYMENT_EXPIRATION_MINUTES
TURBORAMA_ADMIN_USERNAME
TURBORAMA_ADMIN_PASSWORD_HASH
TURBORAMA_ADMIN_PUBLIC_HOST
TURBORAMA_ADMIN_KEY_DIRECTORY
```

As duas chaves devem conter 32 bytes aleatórios em Base64 e precisam ser diferentes. `TURBORAMA_ALLOW_HTTP_LOOPBACK=true` é permitido apenas quando o Cloudflare Tunnel acessa a aplicação pela interface local do mesmo servidor.

## Painel e preços compartilhados

O painel fica em `/admin`, mas falha fechado por hostname. Com
`TURBORAMA_ADMIN_PUBLIC_HOST` vazio ele fica totalmente desativado. Quando a variável recebe, por
exemplo, `painelpix.lzgames.com.br`, o painel aceita somente esse nome exato e somente uma requisição
HTTPS encaminhada pelo proxy local confiável. No hostname da API, como `pix.lzgames.com.br`, qualquer
rota `/admin` responde `404` e não redireciona para o login.

A senha não é gravada: o servidor recebe apenas um hash PBKDF2 gerado
interativamente pelo próprio executável com `--hash-admin-password`. A sessão administrativa usa
cookie cifrado, `Secure`, `HttpOnly`, `SameSite=Strict`, expira em 30 minutos e todas as alterações
exigem token antifalsificação.

Os valores de 15, 30, 45, 60 e 120 minutos possuem uma configuração central por licença. O site e o
EmulationStation não compartilham fisicamente um arquivo de disco: ambos leem e gravam o mesmo
registro versionado do servidor. O `owner-settings.json` do gabinete é somente o cache local
protegido dessa configuração. Assim:

- uma alteração no site chega ao gabinete na próxima sincronização;
- uma alteração no EmulationStation é enviada ao site quando a máquina possui permissão;
- se os dois forem alterados ao mesmo tempo, a versão mais nova do servidor vence e evita sobrescrita;
- na primeira atualização, os preços que já existem no estado são preservados e recebem versão;
- o painel pode bloquear novas cobranças sem apagar os preços existentes.

O hostname administrativo deve ser criado primeiro como aplicação protegida pelo Cloudflare Access
e com validação do token no `cloudflared`; somente depois ele pode ser ligado ao túnel e colocado em
`TURBORAMA_ADMIN_PUBLIC_HOST`. Não coloque a rota da API `/v1/*` atrás do login humano. O login do
próprio TurboRama continua obrigatório como segunda barreira.

Consulte [a arquitetura completa](docs/ARQUITETURA-LICENCIAMENTO-ONLINE-PIX-v25.md) e [a implantação isolada no Linux](deploy/linux/README.md) antes de iniciar qualquer serviço.

## Administração Mercado Pago

O argumento usado como caixa é o `external_id` definido quando o PDV foi criado no Mercado Pago. Ele
não é Public Key, Client ID, User ID nem o ID numérico interno do caixa. Segundo a documentação oficial,
deve ser alfanumérico e possuir menos de 40 caracteres.

`--set-mercadopago CLIENTE EXTERNAL_POS_ID` solicita o Access Token por entrada oculta, consulta
`GET /pos?external_id=...` e só grava a credencial cifrada se encontrar exatamente o caixa informado.
Essa consulta não cria order, QR ou cobrança. Depois, `--validate-mercadopago CLIENTE` permite repetir
a validação usando a credencial já protegida no estado do servidor.

## Regras de segurança

- mantenha este repositório privado;
- renove imediatamente qualquer credencial que já tenha aparecido em conversa, captura de tela ou histórico;
- não reutilize Access Token de teste em produção;
- nunca exponha diretamente a porta local da aplicação à internet;
- faça backup cifrado do estado e das duas chaves e teste a restauração;
- não execute exemplos de implantação sem revisar o site e o túnel Cloudflare que já estão funcionando.

O endpoint `POST /v1/orders`, o cabeçalho de idempotência e a consulta `GET /v1/orders/{order_id}` seguem a documentação oficial atual do Mercado Pago.

## Troca de placa-mãe ou reinstalação

O painel possui uma ação específica para transferência de hardware. Depois de confirmar novamente a
senha administrativa, o servidor suspende as sessões e os vínculos anteriores, coloca a licença em
`TRANSFER_PENDING` e mostra um código de ativação de uso único. A instalação nova precisa provar a
posse de sua chave privada e apresentar esse código. Somente depois da prova válida o servidor volta
a licença para `ACTIVE` e autoriza uma única máquina. Uma tentativa inválida não conclui a
transferência nem cria sessão.

Não use apenas **Gerar novo código de ativação** para trocar placa-mãe. Essa opção continua destinada
a uma máquina adicional quando a licença possui vaga. Para substituição, use **Transferir para outro
hardware e mostrar código único**.
