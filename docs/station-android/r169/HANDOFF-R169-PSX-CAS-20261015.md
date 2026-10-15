# R169 — cadastro aditivo do PSX CAS no servidor

**Estado em 15/10/2026:** esta é uma entrega de documentos e kit privado; **não é implantação R169**. A [R168 foi aplicada](https://github.com/luziellacerda/Servidor-pix/blob/82c81df9c042f1470a6b94e4cede7059c9db1865/docs/station-android/r168/server-return.json) com `serverApplied=true`, 91 motores, 16.896 perfis e 11.981 modos; o limite WinBack foi corrigido e conferido. Contudo, a R168 provocou uma exceção fatal no Android ao iniciar PSX online; [o incidente sanitizado](https://github.com/luziellacerda/Servidor-pix/blob/34aa3d92d49e70a3e1eeefaaff3e67ed1de98120/docs/station-android/r168/incidents/INCIDENTE-R168-PSX-NOSUCHMETHOD-20261015.md) impede usá-la como cliente de teste. A R169 corrige o ponto Android, mas **nenhuma partida PSX R169 foi testada**. No A56 com R169, *Capcom vs. SNK — Millennium Fight 2000 Pro* mostrou exatamente **“não há modo aprovado para esta edição e o motor instalado”**. O cadastro e a requalificação R169 ainda precisam ser comprovados no servidor.

## Entrega exata

- [Kit privado R169 sem APK](https://github.com/luziellacerda/TurboramaEmulationStation/tree/5158d84add5e8efd03c020cf7561f8fc100729a1/station-server-handoffs/r169), commit `5158d84add5e8efd03c020cf7561f8fc100729a1`: [ZIP independente](https://github.com/luziellacerda/TurboramaEmulationStation/blob/5158d84add5e8efd03c020cf7561f8fc100729a1/station-server-handoffs/r169/Kit-Servidor-TurboStations-R169-Independentes.zip), SHA-256 `859b33b156698abecadadf035f8117a4b2b680439c4fed32eb2001347dc6c832`, 14.918.062 bytes, 54 arquivos, 27 ELF AArch64. O [recibo privado](https://github.com/luziellacerda/TurboramaEmulationStation/blob/5158d84add5e8efd03c020cf7561f8fc100729a1/station-server-handoffs/r169/server-kit-receipt-r169.json) traz manifesto, hashes e `serverApplied=false`.
- APK assinado R169 **fora do Git**, SHA-256 `68521db5fbd46d6da9895c4d9b291febda5597c82ba3b51935ccd7e98520877d`. Assinatura v2/v3 com certificado original e alinhamento de 16 KiB foram conferidos na prova do kit. Dois telefones receberam esse APK preservando dados; o hash instalado bateu em ambos e o painel inicial abriu nos dois. Isso não prova gameplay.
- APK R168 doador SHA-256 `875fb0696c4fb50ce4af953bccd62fcd68dfdecb247f65d2fc027b71aa330b3d`; kit R168 doador SHA-256 `c0de71c3a4e10cce320f7ba6966e183871760157ca67e9010eb5bdfb0850177b`. A R168 serve somente de baseline técnica para o delta; não deve ser cadastrada.

## Delta fechado e identidade

Somente cinco entradas do APK mudaram: `classes35.dex`, `assets/station-online/engines.json` e três marcadores de identidade (`libstation_psx_online.so`, `libstation_mupen64plus_runtime.so`, `libstation_dolphin_runtime.so`). Em `classes35.dex`, o recibo registra a troca de uma instrução CAS no offset 572034, sem alteração de definições de classes, threads, timers ou polling. As outras 39 partições DEX, o carrossel, os núcleos nativos, 21 dependências, políticas e opções N64 permanecem preservados. As quatro identidades mudaram porque os marcadores vinculam o APK final, mesmo sem alteração funcional do N64, GameCube ou Wii.

| Plataforma | `engineId` R169 exato |
| --- | --- |
| PSX | `pcsx-rearmed-ff81ed17-player-v3-cddba501a25c` |
| N64 | `mupen64plus-ae-33bf7021-n64-v3-a69ac794a2d1` |
| GameCube | `dolphin-5102a033-gamecube-v3-48da63940e83` |
| Wii | `dolphin-5102a033-wii-v3-48da63940e83` |

## Ação pedida ao servidor

O código e o preparador para revisão estão no ramo privado
[`fix/station-r169-register-20261015`](https://github.com/luziellacerda/Servidor-pix/tree/2a0b051/docs/station-android/r169/operator),
commit `2a0b051`. Este ramo é preparação; **não é implantação**.

1. Conferir o SHA-256 do ZIP e todos os 54 itens do manifesto, incluindo `engines-apk.json`, 27 ELF, dependências, políticas e recibos. Não pedir APK, chave ou logs brutos.
2. Partir da baseline R168 **realmente aplicada**, registrada no recibo do commit `82c81df`; não repetir o cadastro R168. O limite WinBack já foi corrigido para 256 KiB no servidor e validador. Confirmar que a configuração ativa corresponde ao recibo antes de projetar R169. Não apagar versões antigas, relaxar aprovação de modos ou contornar a trava.
3. **Corrigir a capacidade antes de cadastrar R169.** O pré-voo R168 já usou 16.762.688 bytes formatados de perfis de um teto 16.777.216 e 8.126.781 bytes compactos de modos de um teto 8.388.608. Mais 492 vínculos R169 excedem ambos. O [operador R169](https://github.com/luziellacerda/Servidor-pix/tree/fix/station-r169-register-20261015/docs/station-android/r169/operator) inclui limites coordenados de 17 MiB para perfis e 9 MiB para modos nos produtores, registrador transacional, importador e leitor C#. Seu pré-voo deve medir os bytes reais e confirmar que o leitor **ativo** aceita o arquivo compacto publicado. O aumento adicional de 128 para 256 motores e de 512 KiB para 1 MiB no manifesto é reserva solicitada para sistemas futuros; a R169 adiciona apenas quatro motores reais (91→95). Git preparado não atualiza automaticamente os scripts/DLL de produção. Resolver separadamente os caminhos **ativos** do scanner e do registrador; cada um deve importar a cópia corrigida de `station_online_profiles.py` no próprio diretório. Fazer backup, aplicar o conjunto compatível, conferir hashes ativos e só então publicar os registros. Até lá, `serverApplied=false` para R169.
4. Cadastrar os quatro owners R169 **aditivamente** e requalificar apenas modos por tupla exata de `itemId`, hash de conteúdo, `engineId`, core, runtime e perfil. Preservar R167/R168, N64, GameCube/Wii, limites de jogadores, licenças e recusas. Não aprovar pelo nome do jogo nem pelo número de modos de referência do kit. Usar o preparador R169 acima, que exige os quatro hashes privados da autoridade R168 e recusa mudanças na baseline, antes da publicação transacional.
5. Entregar recibo **APLICADO**, `serverApplied=true`, com hash do kit aceito, commit efetivamente executado, baseline e contagens antes/depois, tamanhos de perfis/modos e versão do leitor ativo, quatro IDs, modos PSX admitidos e consulta pública **autenticada e assinada** via `POST /v1/station/online/multiplayer/command`, ação `capabilities`, confirmando a edição de teste no motor R169. HTTP 200 isolado, código preparado e sala `playing` não comprovam partida física.

O [auditor independente do catálogo](https://github.com/luziellacerda/Servidor-pix/tree/db348905ebe8b0a363ee67fe7abec0552dc78903/docs/station-android/r169/coverage-audit) já está disponível em ramo separado, **somente leitura**. Deve examinar as 4.744 edições com os quatro exports privados atuais e distinguir modo multiplayer comprovado, classificação individual, recusa explícita, aprovação genérica sem proveniência e lacuna real. Não autoriza modos adicionais nem atrasa o cadastro exato de Capcom VS R169.

## Piloto Android após recibo aplicado

Usar os dois telefones já instalados com R169 para testar *Capcom vs. SNK — Millennium Fight 2000 Pro*, `itemId=station_767e5e92f2f353479e6120f1c28f96a7`, conteúdo SHA-256 `6782ccca63a19cbc6760ca928b3c36d0e6ac4b3e479c722083c96111bd25cc73`, modo VS de dois jogadores. Observar primeiro quadro e áudio nos dois, controles independentes, saída do anfitrião e retorno do convidado por pelo menos 25 segundos. Até essa prova, PSX online R169 permanece candidato, sem declaração de estabilidade.
