# R157: mesmas três ondas azuis na sala online

O usuário aprovou a tela principal R156 e pediu que a tela de salas online
usasse o mesmo desenho: três faixas largas, preenchidas, em tons de azul escuro
até azul claro, com movimento independente. A captura da R156 no Galaxy A56
mostrou que a sala ainda tinha quatro traços finos estáticos.

Esta revisão altera somente `StationHyperspaceView` em `classes35.dex`, no ramo
`ONLINE_WAVES`. O ramo `SKY`, os demais fundos, o menu e o cliente de conexão
continuam como na R156. A geometria e os tempos vêm de
`drawNativeOnlineRibbons` da R156; o desenho da sala usa três caminhos
pré-alocados e só agenda quadros enquanto a tela está anexada, visível,
resumida e com foco. Ao sair ou esconder a sala, a tarefa de animação é
cancelada. O custo da sala online aumenta em relação ao fundo estático R156;
uma medição física de bateria e processamento ainda precisa ser feita.

O APK doador deve ser exatamente a R156 assinada, SHA-256
`a072af70c04f0b9f9455b22a856bcdf0995b42da5650c2f9ee87243d17f5d5a6`.
O empacotamento permite mudar apenas cinco entradas de 13.860:
`classes35.dex`, três marcadores dos motores independentes e
`assets/station-online/engines.json`. As outras 39 partições DEX,
`libturbo_carousel.so`, `libstation_frontend.so`, os motores e os dados de
jogos devem permanecer idênticos. Os marcadores acompanham o novo hash do DEX.

Em um teste de entrada na sala R156 houve um HTTP 409
`STATION_REQUEST_PROOF_REPLAY` em `online/events`. Os 40 DEX da R156 são
idênticos aos da R152; a alteração visual R156 não mudou o cliente online.
O servidor rejeita uma prova válida repetida, e a origem da repetição ainda
não foi demonstrada. R157 não relaxa essa proteção nem altera a rede. Se o
erro voltar, correlacionar horário UTC, rota, código e identificadores de
requisição entre cliente, proxy e servidor sem registrar credenciais.

Critério de liberação: conferir o APK assinado, a tela de salas no A56 e a
interrupção da animação ao sair. O kit R156 permanece retido; um kit R157
precisa de validação e cadastro próprios.
Não declarar a aplicação no servidor nem o jogo físico apenas pela compilação.
