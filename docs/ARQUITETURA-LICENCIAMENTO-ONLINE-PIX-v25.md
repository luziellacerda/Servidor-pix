# TurboRama PIX v25 — licenciamento on-line para máquinas com e sem TPM

Este documento descreve a extensão on-line do TurboRama PIX. Ela não substitui silenciosamente a
proteção TPM existente. O perfil de proteção é explícito, fica registrado no servidor e não pode ser
rebaixado automaticamente quando o TPM deixa de responder.

Este repositório contém somente o backend e o contrato compartilhado. O código do agente, o emissor
de licenças, as chaves privadas e os instaladores do consumidor permanecem fora dele.

## Estado implementado

- `TPM_BOUND`: identidade de assinatura RSA-PSS-SHA256 em chave CNG não exportável do Microsoft
  Platform Crypto Provider. A chave do cofre continua separada da chave de identidade on-line.
- `SOFTWARE_BOUND_ONLINE`: identidade RSA-PSS-SHA256 persistente e não exportável por política no
  Microsoft Software Key Storage Provider, vinculada ao usuário Windows. Este modo exige o servidor
  para toda nova cobrança e tem segurança inferior à proteção em hardware.
- `USB_TOKEN_BOUND`: nome e contrato reservados, mas falha fechado até que um modelo de token, seu
  KSP/SDK e o comportamento sem interação depois do boot sejam escolhidos e testados.
- servidor ASP.NET Core separado do pacote do cliente;
- cadastro por código de ativação de uso único;
- `DeviceId = SHA-256(SPKI DER da chave pública)`;
- desafio aleatório de 256 bits, validade de 60 segundos e consumo único;
- assinatura do desafio vinculada à licença, máquina, sessão, ação e hash canônico da operação;
- sessão exclusiva com lease de 180 segundos. Um clone não derruba a sessão original;
- criação e consulta de cobranças somente pelo servidor;
- tabela de preços fixada no servidor por licença; o cliente não consegue autorizar outro valor;
- idempotência derivada de licença, máquina e referência externa;
- estado persistente com HMAC-SHA256 e promoção atômica;
- Access Token separado por cliente, cifrado com AES-256-GCM no estado do servidor;
- respostas públicas genéricas e motivos internos específicos;
- limite global de requisições e limite de 64 KiB por corpo HTTP.

## Regra de não rebaixamento

O instalador pode detectar recursos, mas o servidor fixa um destes valores na licença:

```text
TPM_BOUND
USB_TOKEN_BOUND
SOFTWARE_BOUND_ONLINE
```

Uma licença `TPM_BOUND` apresentada como `SOFTWARE_BOUND_ONLINE` é recusada com
`BINDING_DOWNGRADE_DENIED`. A mudança legítima exige uma operação administrativa de transferência e
uma nova ativação.

## Fluxo de cobrança

1. O agente abre ou renova uma sessão exclusiva.
2. Para uma cobrança, envia somente LicenseId, DeviceId, SessionId, ação e hash do contexto.
3. O servidor entrega um nonce de uso único.
4. A máquina assina nonce, licença, máquina, sessão, ação, valor, moeda, minutos, expiração e
   referência idempotente.
5. O servidor verifica licença, cliente, dispositivo, perfil, sessão, assinatura e correspondência
   exata entre minutos e a tabela de preços da licença.
6. O servidor abre a credencial Mercado Pago do cliente dentro do processo e cria a order.
7. O gabinete recebe somente QR Code, identificador, valor e estado.
8. A confirmação é consultada pelo mesmo canal autenticado. O gabinete nunca recebe o Access Token.

Sem servidor, novas cobranças falham fechadas. Sessões já pagas e créditos já confirmados continuam
sendo reconciliados localmente pelo comportamento existente do agente.

## Variáveis secretas do servidor

Configure as variáveis no cofre de segredos da hospedagem, nunca no Git ou em arquivos distribuídos:

```text
TURBORAMA_SERVER_STATE_FILE      caminho absoluto do estado
TURBORAMA_SERVER_STATE_KEY       32 bytes aleatórios em Base64, para HMAC
TURBORAMA_SERVER_SECRET_KEY      outros 32 bytes aleatórios em Base64, para AES-GCM
TURBORAMA_PAYMENT_EXPIRATION_MINUTES
```

As duas chaves precisam ser diferentes. Perder qualquer uma impede abrir o estado. Guarde cópias
protegidas fora do servidor e teste a restauração.

`TURBORAMA_ALLOW_HTTP_LOOPBACK=true` existe apenas para testar diretamente a saúde do servidor em
laboratório local. O agente comercial exige HTTPS inclusive quando o endereço é local.

## Administração inicial

Os comandos abaixo são executados no computador/servidor privado. O estado e as chaves de ambiente
precisam estar configurados antes. Pare o serviço durante a administração: o arquivo de estado possui
bloqueio exclusivo entre processos e recusa comandos paralelos para evitar perda de atualização.

Criar uma licença e obter o primeiro código, exibido uma única vez:

```powershell
dotnet .\TurboRamaPixOnlineServer.dll --create-license CLI-0018 TR-000125 SOFTWARE_BOUND_ONLINE 1
```

Fixar a tabela autorizada da licença, em centavos e na ordem 15, 30, 45, 60 e 120 minutos:

```powershell
dotnet .\TurboRamaPixOnlineServer.dll --set-prices TR-000125 750 1500 2250 3000 6000
```

Sem os cinco preços no servidor, nenhuma cobrança nova é autorizada. O arquivo do quiosque precisa
usar exatamente a mesma tabela, mas ele nunca é a autoridade financeira.

Cadastrar a conta Mercado Pago do cliente. O token é solicitado por entrada oculta e não deve ser
colocado na linha de comando:

```powershell
dotnet .\TurboRamaPixOnlineServer.dll --set-mercadopago CLI-0018 TURBORAMAPDV01
```

Emitir outro código de uso único para uma operação autorizada futura:

```powershell
dotnet .\TurboRamaPixOnlineServer.dll --issue-activation-code TR-000125
```

Consultar máquinas e aplicar somente ações administrativas declarativas:

```powershell
dotnet .\TurboRamaPixOnlineServer.dll --list-devices TR-000125
dotnet .\TurboRamaPixOnlineServer.dll --set-license-status TR-000125 SUSPENDED
dotnet .\TurboRamaPixOnlineServer.dll --set-device-status TR-000125 DEVICE_ID SUSPENDED
dotnet .\TurboRamaPixOnlineServer.dll --force-reauth TR-000125 DEVICE_ID
```

Os estados aceitos para licença são `ACTIVE`, `SUSPENDED`, `REVOKED`, `MAINTENANCE` e
`TRANSFER_PENDING`. Para máquina são aceitos os quatro primeiros. Suspender, revogar ou colocar em
manutenção encerra as sessões atuais. O servidor não possui mecanismo para enviar PowerShell,
executável, script ou código arbitrário ao quiosque.

## Configuração do agente

O cadastro protegido do quiosque deve selecionar:

```json
{
  "provider": "online",
  "onlineBaseUrl": "https://licencas.exemplo.com/",
  "onlineLicenseId": "TR-000125",
  "onlineProtectionProfile": "SOFTWARE_BOUND_ONLINE"
}
```

Esse cadastro pode ser criado sem colocar segredos no arquivo. Prepare, por exemplo,
`configurar-online.json`:

```json
{
  "schemaVersion": 1,
  "baseUrl": "https://licencas.exemplo.com/",
  "licenseId": "TR-000125",
  "protectionProfile": "SOFTWARE_BOUND_ONLINE",
  "packagePricesCents": { "15": 750, "30": 1500, "45": 2250, "60": 3000, "120": 6000 }
}
```

Na conta Windows automática do quiosque:

```powershell
dotnet .\TurboRamaPixAgent.dll --online-configure .\configurar-online.json
```

O agente valida HTTPS, licença, perfil e preços antes de promover `owner-settings.json`. O arquivo
temporário não contém Access Token, Client Secret ou código de ativação.

Esse deve ser o primeiro comando no quiosque novo: o agente aplica o perfil escolhido antes de criar
a chave da máquina e antes de gerar o pedido da licença local. Em seguida, use `--license-request`,
emita a licença no computador privado e instale-a com `--install-license`, conforme o manual privado
do cliente.

Depois da licença local assinada ser instalada, a ativação lê o código sem exibi-lo ou salvá-lo:

```powershell
dotnet .\TurboRamaPixAgent.dll --online-activate
```

No provider `online`, os comandos locais de cadastrar/receber Access Token e preparar o editor de
credencial são recusados. Um arquivo antigo de atualização de token também não é consumido. A única
credencial Mercado Pago fica cifrada no servidor.

## Compilação do servidor

O servidor nunca entra no instalador do consumidor. Em Git limpo:

```powershell
.\COMPILAR-SERVIDOR-PIX-ONLINE.ps1 -RuntimeIdentifier portable
```

O script compila com avisos tratados como erro, executa o autoteste, publica sem PDB/fonte/segredo e
gera `CHECKSUMS-SHA256.txt`. A saida `portable` exige o runtime .NET 8 na hospedagem. Os alvos
`win-x64` e `linux-x64` continuam disponiveis, mas precisam dos pacotes de runtime correspondentes
na maquina de compilacao.

## Limitações que permanecem

- `SOFTWARE_BOUND_ONLINE` não consegue oferecer prova de hardware. Administrador com controle total
  pode tentar copiar ou instrumentar a chave de software. A barreira decisiva é o servidor não
  disponibilizar a credencial financeira e exigir sessão exclusiva em cada cobrança.
- fingerprint de BIOS/placa/MachineGuid é um sinal de risco, não uma raiz criptográfica. Ele pode
  gerar falso positivo após manutenção e pode ser falsificado por um atacante avançado.
- a alegação remota de `TPM_BOUND` ainda precisa de atestação TPM verificável para o servidor provar,
  sem confiar no cliente, que a chave foi gerada no hardware.
- o repositório de estado atual é transacional e autenticado para uma instância, mas não substitui um
  banco gerenciado com replicação, backup, monitoramento e alta disponibilidade.
- a administração implementada nesta fase é por comandos locais no servidor. Um painel Web com
  autenticação multifator, perfis de acesso e trilha de auditoria externa ainda não foi construído.
- o fluxo OAuth Mercado Pago para autoatendimento do cliente ainda precisa de domínio público,
  callback HTTPS e aplicação comercial registrada. Até lá, o cadastro do token é feito no servidor
  privado por entrada oculta.
- nenhuma entrega deve ser vendida antes de hospedar com TLS válido, backup restaurado em teste,
  monitoramento, política de privacidade/LGPD e teste real controlado do começo ao fim.

## Testes automáticos atuais

O agente verifica serialização HTTP, ativação, RSA-PSS, abertura/heartbeat de sessão, criação e consulta
de cobrança. O servidor verifica ativação de uso único, cofre AES-GCM, prova de posse, preservação da
sessão original, clone concorrente, tabela de preços, alteração de valor, idempotência e replay de nonce.

O formato de cobrança segue a API oficial atual de Orders do Mercado Pago: `POST /v1/orders`,
consulta em `GET /v1/orders/{order_id}`, QR dinâmico e caixa em `config.qr.external_pos_id`.

Referências oficiais:

- <https://www.mercadopago.com.br/developers/pt/docs/qr-code/payment-processing>
- <https://www.mercadopago.com.br/developers/pt/reference/in-person-payments/qr-code/orders/get-order/get>
