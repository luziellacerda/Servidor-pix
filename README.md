# Servidor PIX on-line do TurboRama

Backend privado de licenciamento, prova criptográfica de máquina e administração remota do TurboRama. O servidor autoriza licenças e máquinas; preços, credencial Mercado Pago, PDV, QR Code, confirmação do pagamento e concessão de créditos permanecem no gabinete.

> Este repositório contém código comercial sensível e deve permanecer **privado**. Nunca coloque Access Token, Client Secret, chaves de cifragem, arquivo de estado ou dados de clientes no Git.

## O que está incluído

- API ASP.NET Core em `.NET 8`;
- ativação por código de uso único;
- prova de posse da chave privada com RSA-PSS-SHA256;
- perfis `TPM_BOUND` e `SOFTWARE_BOUND_ONLINE`;
- sessão exclusiva e registro de tentativas de clonagem;
- painel Web administrativo responsivo, com pesquisa, filtros, indicadores operacionais e exportação CSV da auditoria;
- login administrativo, proteção CSRF, sessão curta, confirmação de ações críticas e trilha de auditoria;
- bloqueio remoto de novas cobranças PIX, licenças e máquinas;
- transferência administrativa de licença para outra placa-mãe ou nova identidade, com código único;
- estado autenticado com HMAC-SHA256 e gravação atômica;
- autoteste sem dinheiro real e sem conexão com o Mercado Pago;
- compatibilidade temporária com rotas antigas de preço/pagamento, fora do painel e fora do fluxo atual do gabinete.

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

Use o compilador descrito abaixo. Ele isola cache, objetos, binários intermediários e publicação na
unidade temporária escolhida, executa o autoteste e gera os checksums do pacote.

O autoteste usa um provedor falso e não cria cobranças reais.

## Compilação para o Linux

O pacote portátil é o recomendado para o servidor Linux com ASP.NET Core Runtime 8 instalado. Com o
Git limpo e revisado:

```powershell
.\COMPILAR-SERVIDOR-PIX-ONLINE.ps1 `
  -RuntimeIdentifier portable `
  -DiretorioTemporarioBuild "H:\TurboRamaTemp" `
  -Saida "H:\TurboRamaTemp\servidor-pix-online-portable"
```

A saída padrão fica em `outputs/servidor-pix-online-portable`; uma saída absoluta pode ser informada
como no exemplo. O pacote não contém iniciador específico do Windows e inclui checksums SHA-256. O
alvo `linux-x64` permanece disponível quando for necessário publicar para um runtime específico.

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

## Painel administrativo e autonomia local

O painel fica em `/admin`, mas falha fechado por hostname. Com
`TURBORAMA_ADMIN_PUBLIC_HOST` vazio ele fica totalmente desativado. Quando a variável recebe, por
exemplo, `painelpix.lzgames.com.br`, o painel aceita somente esse nome exato e somente uma requisição
HTTPS encaminhada pelo proxy local confiável. No hostname da API, como `pix.lzgames.com.br`, qualquer
rota `/admin` responde `404` e não redireciona para o login.

A senha não é gravada: o servidor recebe apenas um hash PBKDF2 gerado
interativamente pelo próprio executável com `--hash-admin-password`. A sessão administrativa usa
cookie cifrado, `Secure`, `HttpOnly`, `SameSite=Strict`, expira em 30 minutos e todas as alterações
exigem token antifalsificação.

O painel apresenta licenças, máquinas, ocupação, último contato, tentativas recusadas e auditoria. Ele
permite alterar o estado da licença, autorizar ou bloquear novas compras PIX, exigir nova autenticação,
suspender uma máquina e iniciar a transferência controlada de hardware. As ações críticas exigem
confirmação e ficam registradas.

O painel não edita preço, Access Token, PDV ou provedor bancário. Essas informações continuam no
`owner-settings.json` protegido do gabinete e são administradas pelo software local. Perda de internet,
timeout, DNS ou erro `5xx` não encerram jogos nem retiram créditos; o gabinete preserva a última
autorização local. Somente uma recusa explícita e autenticada pode bloquear uma nova compra PIX.

O hostname administrativo deve ser criado primeiro como aplicação protegida pelo Cloudflare Access
e com validação do token no `cloudflared`; somente depois ele pode ser ligado ao túnel e colocado em
`TURBORAMA_ADMIN_PUBLIC_HOST`. Não coloque a rota da API `/v1/*` atrás do login humano. O login do
próprio TurboRama continua obrigatório como segunda barreira.

Consulte [a arquitetura completa](docs/ARQUITETURA-LICENCIAMENTO-ONLINE-PIX-v25.md) e [a implantação isolada no Linux](deploy/linux/README.md) antes de iniciar qualquer serviço.

## Mercado Pago

O fluxo atual não envia Access Token, PDV ou cobrança ao servidor de licenciamento. O cadastro e a
validação do Mercado Pago são feitos no gabinete pelo configurador local, e o agente local cria e
consulta a cobrança. Rotas e comandos antigos relacionados a pagamento permanecem somente por
compatibilidade de migração e não são exibidos pelo painel profissional.

## Regras de segurança

- mantenha este repositório privado;
- renove imediatamente qualquer credencial que já tenha aparecido em conversa, captura de tela ou histórico;
- não reutilize Access Token de teste em produção;
- nunca exponha diretamente a porta local da aplicação à internet;
- faça backup cifrado do estado e das duas chaves e teste a restauração;
- não execute exemplos de implantação sem revisar o site e o túnel Cloudflare que já estão funcionando.

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
