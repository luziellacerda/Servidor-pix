# TurboRama Online — servidor como autoridade

O servidor LZ Games autoriza máquinas e cria cada nova cobrança PIX. O
EmulationStation continua responsável pela experiência local, pelos preços e
pelos créditos.

## Fluxo de ativação

1. O programa administrativo cria no gabinete uma chave privada vinculada ao
   TPM ou, sem TPM, ao perfil `SOFTWARE_BOUND_ONLINE`.
2. O servidor recebe somente a chave pública e calcula o `DeviceId`.
3. Cada sessão e cada operação usam desafio aleatório de uso único e assinatura
   RSA-PSS-SHA256 da máquina.
4. Cliente ID e License ID nunca bastam para autorizar uma cobrança.

`USB_TOKEN_BOUND` permanece reservado até existir um token criptográfico real
homologado. Pendrive comum não é aceito.

## Cadastro bancário

1. O administrador gera no painel um código bancário de 256 bits, válido por
   15 minutos e uma única utilização.
2. `CONFIGURAR-USER-TOKEN-PIX.exe` consulta a conta e os PDVs reais no Mercado
   Pago e envia Cliente ID, código, PDV escolhido e Access Token ao servidor por
   HTTPS.
3. O servidor valida código, Access Token e PDV antes de consumir o código.
4. O Access Token é cifrado com AES-256-GCM no estado privado do servidor.
5. Um novo cadastro confirmado substitui a única conexão bancária do cliente.

O Access Token não é salvo nem devolvido ao kiosk. Os programas administrativos
são portáteis, ficam com o administrador e não entram no instalador do gabinete.

## Nova cobrança

1. O agente monta o contexto com licença, máquina, sessão, minutos, valor,
   moeda e referência externa.
2. O servidor envia um desafio único.
3. A máquina assina desafio e hash canônico do contexto.
4. O servidor valida licença, máquina, sessão, preço e estado PIX.
5. O servidor cria a cobrança usando sua credencial cifrada.
6. O agente recebe somente identificador, status e QR público.

Não existe fallback local para criar cobrança. Idempotência, expiração e
anti-replay são obrigatórios.

## Indisponibilidade

Sem internet, DNS, túnel ou servidor, somente novas cobranças PIX ficam
indisponíveis. EmulationStation, jogos, créditos já concedidos, F10/F12 e preços
locais continuam funcionando. Uma falha de rede não revoga licença e não encerra
partida.

## Cloudflare

O painel humano em `/admin/*` usa Cloudflare Access e o login próprio do
TurboRama. As rotas `/v1/*` não podem apresentar login humano: elas são
protegidas pelo protocolo criptográfico da máquina, códigos de uso único,
limites de requisição e HTTPS.

No estado verificado em 15/08/2026, `https://painelpix.lzgames.com.br/v1/health`
retornava redirecionamento `302` para o login Cloudflare. Portanto a publicação
da API permanece bloqueada até uma regra específica e mais restrita para
`/v1/*` ou um hostname de API separado ser configurado e testado.

## Limites reais

- controle administrativo completo da máquina pode alterar software local;
- `SOFTWARE_BOUND_ONLINE` é menos forte que TPM;
- QR e status de pagamento são dados públicos da transação, não segredos;
- nenhuma proteção torna um EXE impossível de analisar;
- segurança comercial exige servidor, chaves, backups e repositórios privados.
