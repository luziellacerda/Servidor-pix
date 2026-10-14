## Boot posterior recuperado — 14/10/2026

PCreiniciou18:19UTC−3; volumejogosnãoautomontado causou226/NAMESPACE naAPI. Recuperação aplicada: UUIDcorreto/entradafstabnofail/RequiresMountsFor somenteAPI+scanner, mesmaDLL, API19631ativa/NRestarts0. Capabilities assinadas007owner070241 locais/públicas PASS outra vez,387perfis preservados. Sala anterior não deve ser considerada aberta apósboot. ErroAndroidN64_OWNER_MISSING às18:08/18:09 continua responsabilidade da correção doAPK pela produção. Mesmo handoff/evidência `r157/current-usb-070241-20261014/boot-recovery.json`; autoridade privada /mnt/DADOS/station-n64-new-phone-20261014-204558/boot-recovery-applied.json. NÃOrepetiroperador.

## Atualização USB e falha física do anfitrião — 14/10/2026

O APK atual é `fc1d2d98`, motor N64 `0702412628d2`, já cadastrado e aprovado no servidor. A sala007/Arena foi criada, mas o core do anfitrião fecha com **N64_OWNER_MISSING**. A causa Activity/Application Context foi comprovada nos DEX instalados. A produção está corrigindo o app e analisando o convidado; **gameplay Android ainda não aprovado**. Ler [estado atual](server-return.json) e [falha confirmada](current-usb-070241-20261014/host-crash-confirmed.json). As seções abaixo são históricas e não devem substituir o estado atual.

## Retorno atual do servidor e USB

Cadastro R157 e do APK atual `6203804f` aplicado; compatibilidade do hash Android corrigida. **Sala do 007/Arena criada no Samsung e confirmada no servidor.** Ler [retorno atual](server-return.json) e [diagnóstico resolvido](server-fingerprint-fix/README.md). Os registros de ausência de cadastro abaixo descrevem o instante do repasse original. O ZIP original da produção não foi recebido; os binários exatos foram conferidos na instalação USB autorizada.

# R157 — ondas da sala online e repasse ao servidor

## Identidade e escopo do APK

A R157 foi produzida a partir do APK R156 assinado, SHA-256
`a072af70c04f0b9f9455b22a856bcdf0995b42da5650c2f9ee87243d17f5d5a6`.
A fonte da alteração visual está no commit
`c9906512faf4d80ca5fd6d66880772f02b29a359`. O APK R157 assinado é
`TurboStations-Premium-R157-visual-20261014.apk`, tem 2.715.106.319 bytes e
SHA-256 `563625c40bfce77def6770a67448f83e2206e67a07023c58359ad2a0de4eee34`.
A [receita e descrição do APK](https://github.com/luziellacerda/TurboElden/blob/c9906512faf4d80ca5fd6d66880772f02b29a359/integration/online-wave-corrections-r157/README.md)
documenta o escopo; a prova local detalhada está fora do Git, em
`E:\StationVisualR157-20261014\package\candidate\final-apk-proof.json`.
Assinatura original v2/v3 e alinhamento de 16 KiB passaram na conferência.

Entre 13.860 entradas do APK, somente cinco mudaram: `classes35.dex`,
`assets/station-online/engines.json` e os três marcadores ELF dos motores
independentes PSX, N64 e Dolphin. As outras 13.855 entradas, inclusive 39
partições DEX, permaneceram idênticas à R156. Em `classes35.dex`, só a família
`StationHyperspaceView` mudou; as outras 607 classes conferidas permaneceram
iguais. A mudança desenha três ondas azuis largas e animadas na tela de salas
online, acompanhando o visual da tela principal aprovado na R156. Não altera o
cliente de conexão, os comandos online, a segurança ou o protocolo do servidor.
O custo da animação na sala ainda requer medição física de processamento e
bateria. Não inferir desempenho ou partida jogável da verificação de código.

**Estado deste repasse:** APK compilado, conferido e instalado no Galaxy A56 em
14/10/2026. O hash do `base.apk` no aparelho confere exatamente com o APK
assinado acima; `firstInstallTime` e `dataDir` permaneceram iguais, preservando
os dados. Aceite visual da sala, kit privado R157, entrega USB e cadastro R157
no servidor ainda dependem de recibos próprios. A prova do APK registra
`installed=false` e `serverRegistered=false` no instante da compilação; a
instalação posterior não implica cadastro. O kit R156 foi preparado,
mas está retido; os kits R153/R154/R155 também não devem ser aplicados. A última
aplicação de kit confirmada neste handoff é a R152. Não tratar a R157 como
estável ou pronta para consumidores por causa do APK assinado.

## Recebimento e cadastro quando a R157 for autorizada

1. Receber **somente** o kit privado da R157 final aceita. Conferir o SHA-256
   externo do ZIP, manifesto interno, os quatro `engineId` exatos do
   `engines-apk.json`, seis bibliotecas ELF64 AArch64, vinte dependências
   distintas e os hashes correspondentes às entradas do APK assinado acima.
   Um kit antigo, mesmo com cores iguais, não autoriza o runtime R157.
2. **Primeira transação — 78 modos.** Obter baseline protegido **fresco** do
   catálogo, motores, perfis e modos efetivos e seus bytes/hashes. Usar a
   função `prepare` vigente **do servidor** em
   `docs/station-android/r139/prepare_independent_owners.py`, com a
   proveniência revisada dos 78 modos. Essa função chama o merge com
   `migrate_existing_native_profiles=False`, evitando duplicar vínculos
   existentes. Passar suas saídas ao `prepare` do registrador vigente com os
   ELF reais; conferir o candidato e executar `apply` sob `scan.lock` contra
   essa mesma baseline selada. Guardar o recibo da primeira aplicação e
   confirmar os 66 modos N64, nove GameCube e três Wii vinculados à R157;
   PS1 recebe zero novas aprovações HLE por edição. Não executar o `main` de
   operadores históricos nem copiar um snapshot de registros antigos sobre
   os arquivos efetivos.
3. **Segunda preparação — 321 decisões N64.** Depois do recibo da primeira
   aplicação, obter **outra baseline efetiva fresca**, incluindo os perfis e
   modos já publicados para a R157, e conferir seus bytes/hashes. Vincular
   novamente ao `engineId` N64 e hashes reais da R157 as 321
   decisões documentais já revisadas: 157 classificações individuais e 164
   modos multiplayer, para as 157 edições da coleção. Elas já foram aplicadas
   às R151/R152 e devem permanecer intactas; não é uma nova revisão de manuais
   nem um substituto dos 78 modos iniciais. Usar
   [`complete-review.json`](../n64-completo-20261014/complete-review.json),
   SHA-256 `7aa9da840b8dcc1a9ea3404280d9866dfc7e1b15377228be6cb8bf4800e17908`,
   e [`prepare_complete_n64.py`](../n64-completo-20261014/prepare_complete_n64.py),
   SHA-256 `4fcc40012d77624b82874239669a71bddb7dca880d47e67f72970c7673c70b7c`.
   O ledger e o preparador N64 **não estão no ZIP**: obtê-los no Git do app e
   conferir os dois SHA-256 acima antes de usar. Passar o `engines-apk.json`
   do kit recebido, a **nova** baseline de catálogo/perfis/modos efetivos, o
   módulo de perfis e o diretório R128 **vigentes do servidor**, e gerar a
   saída em diretório novo. O preparador rejeita ROM diferente, edição não
   revisada e política de controles divergente; usa
   `migrate_existing_native_profiles=False`.
4. **Segunda transação — publicação N64 completa.** Passar os `incoming.json`
   e `reviewed-modes.json` dessa segunda preparação ao `prepare` do
   registrador local com as bibliotecas reais do kit. Conferir o candidato e
   executar `apply` sob `scan.lock` contra **essa segunda baseline selada**,
   com autenticação administrativa genuína e rollback do registrador. Guardar
   o segundo recibo, preservando os 78 vínculos R157 da primeira transação,
   os vínculos antigos, licenças, ROMs, capas, scanner e outros serviços.
   Esta entrega de dados não pede nova DLL nem reinício da API. Não reaplicar
   os cadastros R151/R152, a quarentena Wind Waker, o scanner ou outros
   operadores já concluídos.
5. Somente após **os dois recibos reais**, consultar as capabilities assinadas do motor R157
   para `itemId`, conteúdo, `engineId`, `coreSha256`, `runtimeSha256`, perfil e
   `allowedPlayerCounts` exatos. Confirmar as recusas de jogos individuais e
   os modos aprovados, com teste de transporte público se cabível. Registrar
   separadamente o teste físico em aparelhos com a mesma revisão e edição.

Aplicar apenas os 78 modos da primeira etapa preservaria as 321 decisões
anteriores para os motores R151/R152, mas **não daria a cobertura completa ao
motor R157**, pois o vínculo exige identidade e hashes próprios. O total final
de registros deve vir do recibo do servidor, não de uma soma presumida. As
provas públicas com quatro participantes sintéticos verificam transporte;
não homologam emulação ou controles em quatro telefones Android.

## Erro de conexão observado em teste anterior

Ao abrir a sala na R156, houve uma resposta HTTP 409
`STATION_REQUEST_PROOF_REPLAY` em `online/events`. A R156 conservava os 40 DEX
online da R152; esta ocorrência não foi causada por sua alteração visual.
A origem da repetição da prova ainda não foi demonstrada, e a R157 não altera
a rede nem relaxa a proteção contra replay. Se o erro reaparecer, correlacionar
horário UTC, rota, código, identificador da requisição e tentativa entre app,
proxy e servidor, distinguindo uma retransmissão de uma prova gerada novamente.
Sanitizar os registros: não guardar token, cabeçalho de prova, credencial,
licença, chave ou dado pessoal em logs e handoffs. A investigação desse 409 é
separada da aplicação de motores e do aceite visual das ondas.
