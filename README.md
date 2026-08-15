# Servidor PIX on-line do TurboRama

Backend privado de licenciamento, prova criptográfica de máquina, pagamentos
Mercado Pago e administração remota do TurboRama.

> Este repositório é comercial e deve permanecer privado. Nunca publique Access
> Token, Client Secret, chaves do servidor, estado real ou dados de clientes.

## Responsabilidades

- ativação de máquina por código de uso único;
- prova RSA-PSS-SHA256 e sessão exclusiva;
- perfis `TPM_BOUND` e `SOFTWARE_BOUND_ONLINE`;
- transferência controlada de hardware;
- cadastro bancário com código de 256 bits, uso único e validade de 15 minutos;
- Access Token cifrado por AES-256-GCM somente no servidor;
- uma conexão Mercado Pago ativa por cliente;
- criação e consulta de cada nova cobrança PIX;
- validação de preço, licença, máquina, sessão e status antes da cobrança;
- painel administrativo, CSRF, login, confirmação de ações e auditoria;
- bloqueio de licença, máquina e novas compras PIX.

`USB_TOKEN_BOUND` fica bloqueado até a homologação de um token criptográfico
real. Pendrive comum não é aceito.

## Compilação portátil para Linux

```powershell
.\COMPILAR-SERVIDOR-PIX-ONLINE.ps1 `
  -RuntimeIdentifier portable `
  -DiretorioTemporarioBuild "H:\TurboRamaTemp" `
  -Saida "H:\TurboRamaTemp\servidor-pix-online-portable"
```

O compilador executa o autoteste, gera o pacote e grava checksums SHA-256. O
autoteste usa um gateway falso e não movimenta dinheiro.

## Variáveis privadas do serviço

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

As chaves de integridade e de segredos contêm 32 bytes aleatórios em Base64,
devem ser diferentes e precisam de backup cifrado separado. O arquivo de estado
e as chaves nunca entram no Git.

## Painel e API

- painel humano: `/admin/*`, protegido pelo Cloudflare Access e pelo login do
  TurboRama;
- API dos gabinetes: `/v1/*`, sem login humano, protegida pela assinatura da
  máquina, desafio de uso único, sessão, código de cadastro, HTTPS e rate limit;
- `/admin` falha fechado quando hostname e HTTPS não correspondem à configuração;
- o túnel deve alcançar apenas a porta local do serviço; a porta não é publicada
  diretamente no roteador.

Em 15/08/2026 o teste externo real de `/v1/health` em
`painelpix.lzgames.com.br` retornou `302` para o login Cloudflare. Antes da
publicação, restrinja o aplicativo Access a `/admin/*` e crie uma regra mais
específica para `/v1/*`, ou use um hostname de API separado. O teste obrigatório
é `/v1/health` retornar JSON, nunca HTML nem redirecionamento.

## Programas Windows

- `CONFIGURAR-ACCESS-TOKEN-PIX.exe`: ativa licença e identidade da máquina;
- `CONFIGURAR-USER-TOKEN-PIX.exe`: consulta Mercado Pago e cadastra token/PDV no
  servidor usando código bancário de uso único.

Ambos são portáteis e exclusivos do administrador. Não entram no payload do
kiosk. No gabinete permanecem apenas EmulationStation e `pix-agent`.

## Indisponibilidade

Sem internet ou servidor, novas cobranças PIX falham fechadas. Jogos, créditos
já concedidos, F10/F12, preços locais e funções do EmulationStation continuam.
Não existe fallback local de pagamento e uma falha de rede não revoga licença.

## Operação segura

- torne os repositórios privados antes do uso comercial;
- revogue credenciais expostas em conversa, captura, log ou histórico;
- faça backup cifrado e teste restauração das chaves e do estado;
- não altere o site ou o túnel existente sem handoff, backup e rollback;
- não declare teste real aprovado até o QR ser criado, pago e confirmado na
  conta Mercado Pago por uma pessoa.

Consulte [a arquitetura](docs/ARQUITETURA-LICENCIAMENTO-ONLINE-PIX-v25.md) e
[a implantação Linux](deploy/linux/README.md).
