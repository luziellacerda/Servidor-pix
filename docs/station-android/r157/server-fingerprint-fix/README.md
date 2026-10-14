# Correção do aviso de classificação do 007

## Prova e alteração aplicada

O DEX35 real do APK foi carregado pelo `app_process` no Samsung, usando um harness somente de leitura e fixtures sem credenciais. Com o perfil anterior, os modos individual e Arena falhavam em `StationMultiplayerProfile.java:43`, na comparação do hash. O método `canonical` do próprio DEX produziu `SN64\/1` dentro da string JSON; o Python produzia `SN64/1`. O Android implementa esse escape em [JSONStringer.string](https://android.googlesource.com/platform/libcore/+/refs/heads/main/json/src/main/java/org/json/JSONStringer.java).

| Configuração idêntica | SHA256 |
| --- | --- |
| Representação antiga Python | `2966309729f8a19c03b564f0178f263170e1843172f6719002f35fb54e313f9c` |
| Bytes do Android, agora publicados | `688d519ae641911804fe3286e6b5f50cc7f8c06c42ccb1be36781f0fb067b83d` |

Foram alterados apenas `profileSha256` de1.425 registros N64 de sete owners e o código que calcula esse fingerprint. Nenhum modo individual ganhou vagas e nenhuma aprovação foi inferida. A API assinada local/pública recusou o fingerprint antigo com409/`STATION_MULTIPLAYER_PROFILE_UNAPPROVED`, o runtime errado com409/`STATION_MULTIPLAYER_GAME_UNCLASSIFIED` e uma sala no modo solo com409/`STATION_MULTIPLAYER_CAPACITY_UNSUPPORTED`.

Os quatro motores exatos R157 foram registrados em78+321; a atualização que chegou ao Samsung durante a reconexão também teve seus quatro motores registrados em78+321, com baseline nova entre as duas transações. Cores e dependências foram verificados nos ELF reais. O novo APK foi instalado externamente às15h57min31s; este trabalho não o instalou nem mudou seus dados. Seu número de versão/commit de produção ainda não foi publicado; identificar pelo hash completo e pelo engineId do [retorno atual](../server-return.json).

## Como o app deve consumir

1. Consultar capabilities autenticadas em `POST /v1/station/online/multiplayer/command`, com `action=capabilities`, `requestId` novo, `itemId` exato e `clientMaximumPlayers=4` ou5. Validar assinatura, identidade da resposta, `multiplayerVersion=3` e `capability=station-multiplayer.v3`.
2. Selecionar somente o perfil aprovado que coincida com `itemId`, `contentSha256`, `engineId`, `coreSha256` e `runtimeSha256` reais. `profileSha256` é o hash dos bytes canônicos do Android; não usar JSON serializado por outra biblioteca sem conferir os bytes.
3. Para o 007 desta edição, oferecer **Arena**, com2,3 ou4 pessoas conforme `allowedPlayerCounts`. O perfil solo tem `maximumPlayers=1` e vetor vazio: serve à classificação e não cria sala. O cabeçalho agregado não substitui as regras do modo.
4. Criar/entrar com a mesma tupla, incluindo `profileId` e `profileSha256`. Preservar os slots independentes, presença, Pronto, barreira, tickets, prova de requisição e recuperação `station-stream.v3`. Não misturar motores entre os celulares.
5. Uma atualização do APK pode mudar os motores mesmo conservando o layout/DEX e a versão Android visível. Conferir os hashes do APK e de `assets/station-online/engines.json`; registrar as novas identidades a partir dos binários exatos, pela preparação vigente. Não aceitar um runtime por semelhança do nome ou por core igual.

## Evidências e limites

- [Sala criada no Android e conferida no servidor](physical-room-created.json).
- [Publicação da correção](fingerprint-published.json) e [prova assinada da correção](fingerprint-signed-proof.json).
- [Prova do transporte público para quatro participantes na instalação atual](current-four-participant-transport.json).
- O parser real aceitou387 perfis/157 edições/120 multiplayer/zero falhas. As37 edições individuais permanecem individuais. Classificação por máximo:85 até4, duas até3 e33 até2; cada modo conserva sua própria lista de vagas.
- Seis testes de regressão de hash/migração e17 de renovação passaram. A preparação com todos os registros reais permaneceu idempotente. A publicação usou backup fresco, `scan.lock`, substituição atômica e rollback, sem reinício da API.
- A criação foi observada no Samsung. Estes recibos não afirmam gameplay físico em dois/quatro aparelhos ou desempenho de todos157 jogos. O transporte com quatro clientes sintéticos e os testes no DEX são provas distintas.

O `ProfileProbe.java` reproduz a verificação usando o DEX real. Fixtures públicas contêm somente o jogo e a configuração; logs completos, APK/ELF/DEX, serial, capturas e credenciais ficam fora do Git. O operador aplicado é arquivado somente no repositório servidor e não deve ser reexecutado: ler primeiro os recibos atuais.
