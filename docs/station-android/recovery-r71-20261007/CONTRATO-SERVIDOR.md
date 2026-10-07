# Station R71: integração do servidor

## Autoridade e versão

API `https://app.lzgames.com.br`; relay `/v1/station/online/relay`.
`turbobox.lzgames.com.br` é o painel, não a autoridade da API Android.
A implementação e os motores R71 são aditivos. O recibo de produção, emitido
depois dos testes HTTPS/WSS, identifica o binário e a configuração efetivamente
ativos. Este contrato sozinho não comprova implantação ou gameplay físico.

Os dois motores Windows recebidos na entrega R71 são:

- `bsnes-mercury-performance-79d7f9de-rs2-d66267cd4250` (SNES).
- `clownmdemu-d43c2708-rs2-d66267cd4250` (Mega Drive).

Ambos usam runtime SHA-256
`d66267cd42507388f86034deb47e9f9670875784efb9afabfb8ffc64e3f3a856`
e `recoveryProtocol: "station-stream.v2"`. Os hashes dos cores constam no
arquivo recebido `entrega-app-r71-20261007/evidence/server-engine-registry-additions.json`.
Preservar integralmente os quatro IDs antigos. Não registrar geolith, cujo
`launchReady` recebido é falso. Os dois telefones precisam da R71 compatível;
um novo runtime não pode entrar em uma sala de motor R70/v1.

## Recuperação

Aplica-se o protocolo de bytes/offsets/ACK, HELLO, pausa nativa e barreira dos
dois descrito em `../recovery-r67-20261007/CONTRATO.md`.
O snapshot autenticado e assinado anuncia `station-stream.v2` em
`recoveryCapabilities`, `relay-wss-v2` em `transports` e os motores exatos.
Pedidos e tickets continuam exigindo prova RSA-PSS do dispositivo, nonce fresco
e vinculação à sessão/sala/geração. `resume-relay` usa sessão protegida válida e
ticket novo de uso único; não envia `leave` por mera queda física do WSS.

Configuração inicial: 64 salas v2, janela de 262144 bytes por direção,
máximo de 33554432 bytes de anéis retidos. A configuração v1 é preservada.
Estes limites de admissão não são uma homologação de centenas de jogadores.
Reinício do processo do servidor ou do motor perde o estado em RAM.

## Nomes completos da própria sala

Nova capacidade assinada `roomCapabilities: ["short-invite-v1",
"own-room-member-profiles-v1"]`.
O campo `snapshot.room.memberProfiles` existe somente na própria sala e contém
um registro por participante, na ordem de `members`, com `peerId` e `nickname`.
É completo mesmo quando a página social selecionada não contém os membros.
Não expõe nome civil, e-mail, licença, chave ou dados de outro produto.

Exemplo sintético do trecho interno do payload assinado:

```json
{
  "members": ["synthetic-host-id", "synthetic-guest-id"],
  "ready": ["synthetic-host-id", "synthetic-guest-id"],
  "memberProfiles": [
    {"peerId": "synthetic-host-id", "nickname": "Jogador anfitrião"},
    {"peerId": "synthetic-guest-id", "nickname": "Jogador convidado"}
  ]
}
```

O app deve verificar assinatura e identidade antes de usar o campo, associar
`memberProfiles.peerId` aos IDs de `members` e decidir prontidão pelos IDs em
`ready`. O nome não autoriza ingresso nem início. A R71 recebida ainda usa o
cache de nomes; o próximo delta do app deve consumir este campo aditivo sem
rebaixar a composição R71, os DEX, runtime, assinatura ou dados existentes.

## Catálogo e downloads

O índice permanece revisão 14, 2212 jogos visíveis. ID, capa e artefato vêm do
catálogo autenticado, sem inferência pelo nome da pasta. Capas em
`/v1/station/covers/{coverId}`; autorização assinada em
`POST /v1/station/downloads/authorize` com identidade e domínio
`TurboRamaStationAndroid/request-download/v1`; baixar com o grant em
`/v1/station/artifacts/{grantId}`. Manter prova por pedido e credenciais privadas.
Esta atualização não acrescenta varredura de ROMs, recompilação de capas ou
limitação de velocidade ao fluxo de download.

## Evidência

O teste de estado tem 91 verificações, incluindo dois participantes fora da
página social. A qualificação isolada e a verificação pública usam somente duas
licenças descartáveis identificadas por propriedade; confirmam catálogo, capa,
download real, motores, prova antirreplay, ticket único, transferência v2,
renovação de sessão/retomada e v1. Pausa nativa é simulada nesse teste de servidor.
Gameplay em dois Android exige teste físico posterior, identificado pelo
horário, jogo e correlação. Não confundir esse teste com a prova sintética.
