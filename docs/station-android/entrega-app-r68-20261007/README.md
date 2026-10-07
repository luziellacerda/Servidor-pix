# R68 — cantos retos em todas as coleções

Pedido confirmado pelo mantenedor: remover o arredondamento de todas as células na página de coleções, em qualquer plataforma atual ou futura. O carrossel principal conserva seus cantos atuais; as capas individuais de jogos também conservam seu formato.

## Implementação

A página de coleções ativa `folderMode` e `systemsMode`. O raio nativo anterior usava somente `systemsMode`, então recortava os vídeos das coleções em 11,5% da menor dimensão. `collection_corner_policy.h` devolve raio zero quando os dois estados indicam uma coleção. Não há lista de sistemas nem necessidade de atualizar aliases para plataformas futuras.

`native_formation.h::cornerRadius` delega à política. A mesma função alimenta o quadro do vídeo ao vivo, o poster ou frame retido, o fundo enquanto prepara o vídeo e a área de toque. Centro e laterais recebem a mesma regra. “Todos os jogos”, quando exibido na página de coleções, também usa cantos retos.

Dimensões, proporções, UVs, duração, velocidade, decoders, vídeos e posters não mudam. Artes que já contenham bordas desenhadas em seus próprios pixels não são editadas. O limite de 30 fps do carrossel R67 continua.

## Identidade e preservação

- Base R67: `d746cc02b602162b19509cd44ad7cf751e320de3b52199e7d48e86e9e897228f`.
- APK R68: `G:\BAKUP SISTEMA APP 03-10-2026\apks-candidatos-visuais\TurboStations-Premium-R68-20261007.apk`.
- SHA-256 R68: `72ce7c2cbb3d7cf14ad122b0b98e42a41563114bbbc5c925caedbdee49ff66c3`, 2.110.287.488 bytes.
- Mudança única no APK: `lib/arm64-v8a/libturbo_carousel.so`, hash `60b944cb44a61b0c45af67ec084bb37e4ac56b21e7847c8ca7baf45c5dfb4a3d`.
- 13.221 outras entradas comparadas byte a byte: todos os DEX, 55 vídeos, segurança R67, emuladores, controles, runtime online e BIOS automática R66 preservados.
- Certificado original `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`; alinhamento 16 KiB conferido.

## Pastas e reprodução

Fonte desta alteração: este snapshot, sobre a composição nativa completa R67. Build final: `E:\ESTUDO APK\work\station-collection-corners-r68-20261007-final`. A pasta com o mesmo nome sem `-final` contém um protótipo restrito a SNES/Neo Geo, superado pela confirmação do mantenedor; ele não foi empacotado ou instalado.

1. Restaurar/validar os pré-requisitos R67 conforme seu README e manifesto, incluindo os objetos das mídias privadas.
2. Executar `python recipes/build_native.py --output <nova pasta em E:>`; a receita recusa sobrescrever saídas e valida todas as fontes da base.
3. Definir as credenciais de assinatura originais privadamente nas variáveis `STATION_KEYSTORE`, `STATION_KEY_ALIAS`, `STATION_KS_PASS` e `STATION_KEY_PASS`.
4. Executar `python recipes/package_r68.py --workspace <pasta acima> --output <novo APK em G:>`.
5. Atualizar sem desinstalar/limpar dados. A receita `../station-collection-videos-r67-20261007/recipes/install_verified.py --workspace <pasta acima>` aceita o recibo R68, exige base R67 exata, um aparelho autorizado e nenhuma Activity de emulação ativa; confere assinatura através do empacotamento e hash/UID/data depois de instalar.

## Verificações e limites

Passaram 24 verificações da política em quatro combinações de tela e três dimensões, três verificações de ligação aos pontos de desenho/toque, compilação Android ARM64 e comparação integral do APK. A inspeção visual no aparelho está registrada separadamente no recibo de instalação, quando existir; esses testes locais não são uma inspeção visual.

Esta alteração não implementa a retomada online. O pedido REC-01 a REC-09 e o contrato/Java R67 permanecem válidos. Nenhuma chamada, serviço Linux ou motor foi modificado. O implementador que gerar outra versão deve conciliar este pequeno overlay nativo para preservar o pedido visual.

## Instalação e observação física

R68 instalada no Samsung A56 em 07/10/2026 às 19:32:25 UTC; hash integral conferido e mesmo UID/data original. Sem desinstalar, limpar dados ou mudar ajustes. ESActivity abriu e a tela de coleções do SNES foi observada com cantos retos no card principal e laterais. Recibo `evidence/installation-samsung.json`. Sem teste físico de toque nos quatro cantos nem inspeção de todas as plataformas. A captura ficou apenas no PC; não publicada.
