# Servidor PIX on-line do TurboRama

Backend privado de licenciamento, prova de máquina e criação de cobranças PIX do TurboRama. O servidor mantém as credenciais financeiras fora do computador do consumidor e exige autorização on-line para cada nova cobrança.

> Este repositório contém código comercial sensível e deve permanecer **privado**. Nunca coloque Access Token, Client Secret, chaves de cifragem, arquivo de estado ou dados de clientes no Git.

## O que está incluído

- API ASP.NET Core em `.NET 8`;
- ativação por código de uso único;
- prova de posse da chave privada com RSA-PSS-SHA256;
- perfis `TPM_BOUND` e `SOFTWARE_BOUND_ONLINE`;
- sessão exclusiva e registro de tentativas de clonagem;
- tabela de preços controlada pelo servidor;
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
```

As duas chaves devem conter 32 bytes aleatórios em Base64 e precisam ser diferentes. `TURBORAMA_ALLOW_HTTP_LOOPBACK=true` é permitido apenas quando o Cloudflare Tunnel acessa a aplicação pela interface local do mesmo servidor.

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
