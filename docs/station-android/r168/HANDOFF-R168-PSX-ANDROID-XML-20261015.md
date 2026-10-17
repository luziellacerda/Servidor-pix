# R168 — correção Android da abertura PSX online

**Estado em 15/10/2026:** APK R168 assinado e kit privado independente conferidos localmente. O kit informa `serverApplied=false`; ainda não há cadastro R168 nem partida física PSX validada. Este arquivo é handoff para cadastro, não recibo de implantação ou declaração de estabilidade.

**Fontes exatas:** [kit privado sem APK, commit `4733c00f`](https://github.com/luziellacerda/TurboramaEmulationStation/tree/4733c00fbba4be0c8dd5b9c813ba74002732d1b5/station-server-handoffs/r168), com [ZIP independente](https://github.com/luziellacerda/TurboramaEmulationStation/blob/4733c00fbba4be0c8dd5b9c813ba74002732d1b5/station-server-handoffs/r168/Kit-Servidor-TurboStations-R168-Independentes.zip) e recibo; [fonte e receita R168 no TurboElden, commit `e8c0e8c6`](https://github.com/luziellacerda/TurboElden/commit/e8c0e8c6cbeb57202c9193cbe4f3cd394da027ca), ramo `build/station-r168-psx-xml-20261015`. Este ramo do Servidor-pix contém somente documentação e recibo. Não equivale a cadastro dos quatro motores nem a implantação.

## Falha comprovada na R167

Na partida PSX *Capcom vs. SNK — Millennium Fight 2000 Pro* com dois telefones R167, a sala aceitou o início, mas o Android lançou `ParserConfigurationException` em `DocumentBuilderFactoryImpl.setFeature` durante a cópia das preferências privadas. A falha ocorreu antes do anexo ao motor PSX. Reinstalar a R167 não corrige isso. O diagnóstico do servidor está em `Servidor-pix` commit `24d06e5bffbf6b8114ce1405566ae936ca4378f6`, `docs/station-android/r167/diagnostics/psx-android-xml-20261015/`.

O método corrigido usa o leitor SAX compatível com o Android e mantém a recusa de DTD e entidades externas. Ele lê o formato real de preferências do EmulationStation, incluindo elementos irmãos, e grava somente a cópia privada necessária à partida. A prova do servidor incluiu 29 verificações no moto g52 e 64 verificações JVM. Essas verificações não são gameplay com o novo APK.

## Delta fechado da R168

Baseline: APK R167 assinado, SHA-256 `dd6f8f8131cf5ee55d450c60e0b08963a662dc6cad092588f0db50153badd978`; kit privado R167 SHA-256 `82cf0bff9f4ec3083fc395cd6cf38d6d9f227df6aac06b09b6d97882c9947477`. A R167 já foi cadastrada no servidor, commit `cc00a99867dce2189e00b759dbf416198c6b23d4`. O cadastro da R168 será **aditivo**, sem repetir o operador R167.

O contrato de montagem exige exatamente cinco entradas alteradas no APK: `classes35.dex`, `assets/station-online/engines.json` e três marcadores de identidade (`libstation_psx_online.so`, `libstation_mupen64plus_runtime.so`, `libstation_dolphin_runtime.so`). Em `classes35.dex`, somente `StationPsxLaunch` é modificado e `StationPsxLaunch$1` é adicionado; as outras 607 classes mantêm semântica. As outras 39 partições DEX, o carrossel R167 (SHA-256 `82017952a6bf44a7a8c9098a3ef568fb0d2b809d99211be0e17785a77559d22a`), os núcleos nativos, dependências, perfis, limites e opções online devem permanecer byte a byte ou semanticamente idênticos conforme cada prova. **Nenhuma lógica N64 deve mudar.**

| Prova final | Valor conferido |
| --- | --- |
| APK R168 SHA-256 | `875fb0696c4fb50ce4af953bccd62fcd68dfdecb247f65d2fc027b71aa330b3d`, 2.715.483.234 bytes; `F:\StationPsxXmlR168-20261015\final\TurboStations-Premium-R168-psx.apk` |
| Certificado de assinatura | SHA-256 `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`; v2/v3 e alinhamento 16 KiB conferidos |
| Novo `classes35.dex` SHA-256 | `072ec067c3f61e8c9320683ed45dd37cb99b2e6f6851e841a53d6961f60672bc` |
| PSX `engineId` / marcador SHA-256 | `pcsx-rearmed-ff81ed17-player-v3-b21623fee188` / `b21623fee188dabf304c36aff588f56e8f1999e1ee7ad5ecaa90865df5f27cd4` |
| N64 `engineId` / marcador SHA-256 | `mupen64plus-ae-33bf7021-n64-v3-a0c9d15cfbc7` / `a0c9d15cfbc7f84ebe7088de68f99d53419bdf840c4a24429723a4f64460d415` |
| GameCube `engineId` / marcador SHA-256 | `dolphin-5102a033-gamecube-v3-04383e1f63a0` / `04383e1f63a036b590aa22398bc0d2280a3bb04bcf472858f11f9ce4bd3577a9` |
| Wii `engineId` / marcador SHA-256 | `dolphin-5102a033-wii-v3-04383e1f63a0` / `04383e1f63a036b590aa22398bc0d2280a3bb04bcf472858f11f9ce4bd3577a9` |
| ZIP privado sem APK SHA-256 | `c0de71c3a4e10cce320f7ba6966e183871760157ca67e9010eb5bdfb0850177b`, 14.275.389 bytes, 51 arquivos, 27 ELF; `E:\StationPsxAndroidXmlR168ServerKit-20261015\final-kit\Kit-Servidor-TurboStations-R168-Independentes.zip` |
| Recibos | `F:\StationPsxXmlR168-20261015\package\final-apk-proof.json`; `F:\StationPsxXmlR168-20261015\dex\dex-receipt.json`; `E:\StationPsxAndroidXmlR168ServerKit-20261015\final-kit\server-kit-receipt-r168.json` |

Os quatro `engineId` mudaram porque o vínculo cobre o payload assinado. Isso não significa mudança funcional do N64, GameCube ou Wii. Copiar IDs R167 para o cadastro R168 seria incorreto. O pacote final e o kit contêm os mesmos quatro IDs, marcadores e dependências. A prova final preservou 13.856 de 13.861 entradas do APK R167, 39 DEX, carrossel e ponte N64. O recibo DEX registra 608 classes de origem, 609 no candidato, 607 intactas, uma modificada e uma adicionada. Fonte produtiva congelada no commit `e8c0e8c6cbeb57202c9193cbe4f3cd394da027ca`.

## Ação delimitada do servidor

1. Receber o kit R168 **pelo Git privado**, conferir ZIP, manifesto de arquivos, 27 ELF, `engines-apk.json` e prova do APK assinado. O APK e a chave não vão ao Git.
2. Consultar a baseline protegida já aplicada R167. Executar preflight de cadastro R168 com trava e transação, adicionando os quatro novos owners sem apagar R167 ou versões anteriores. Não repetir o cadastro R167 e não modificar o serviço N64.
3. Requalificar somente modos anteriormente aprovados que correspondem à tupla exata `itemId`, hash do conteúdo, `engineId`, core, runtime e perfil; manter recusas, número de vagas, licenças e opções. A referência de 78 modos no kit não aprova automaticamente nada. A R167 registrou 84 edições PSX e 23 modos admitidos; o resultado R168 depende da requalificação real.
4. Confirmar em consulta pública **autenticada** `POST /v1/station/online/multiplayer/command`, `action=capabilities`, com `requestId` novo, os quatro IDs R168 e os modos PSX admitidos, incluindo a edição de teste. HTTP 200 isolado, código preparado ou sala em `playing` não são prova suficiente.
5. Devolver recibo `serverApplied=true` com commit efetivo, hash do kit aceito, baseline e contagens antes/depois, quatro IDs e hashes, decisões requalificadas e prova da consulta pública. Preservar o registro R167, o estado N64 e o reparo de saída do anfitrião já implantado.

## Piloto físico depois do recibo aplicado

Atualizar manualmente dois telefones para o mesmo APK R168 pelo instalador USB permanente, preservando dados. Conferir o hash instalado de cada aparelho. O jogo piloto é *Capcom vs. SNK — Millennium Fight 2000 Pro*, `itemId=station_767e5e92f2f353479e6120f1c28f96a7`, conteúdo SHA-256 `6782ccca63a19cbc6760ca928b3c36d0e6ac4b3e479c722083c96111bd25cc73`, modo `versus`, duas vagas. Observar separadamente: saída da sala ao tocar **Abrir partida**, primeiro quadro e áudio em ambos, controles independentes, saída do anfitrião e aviso/retorno do convidado por pelo menos 25 segundos. Registrar também que a largura das capas vizinhas da R167 permaneceu, sem mudar a capa principal.

Até a observação real, R168 e PSX online permanecem candidatos. O kit ou a consulta do servidor não provam partida física.
