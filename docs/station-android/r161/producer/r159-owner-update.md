# R159 — marcador de posse do motor N64 ao iniciar a partida

## Atualização de 15/10/2026 — servidor R159 pronto; partida física pendente

O [retorno R159 do servidor](https://github.com/luziellacerda/Servidor-pix/blob/docs/station-online-exit-r135-20261012/docs/station-android/r159/server-return.json) confirma o recebimento e a aplicação do APK SHA-256 `4837be706f9ac1f913b4844353741f70c539674e125ae185239611cf5faac2b3`, do ZIP SHA-256 `d1f0b3805392c13b597e85b4ef8ec1fa938c19f77f2e5f5ee6ee3f2948c5454c` e do motor N64 terminado em `7d50f27b4702`. Capabilities assinadas, admissões e transporte público com quatro clientes sintéticos passaram. **Não repetir o cadastro R159.** Ainda não houve partida Android física homologada.

O servidor registrou o relato de tela preta e áudio engasgando em `007: The World Is Not Enough` online, enquanto o jogo offline funciona. Não há captura contemporânea dos dois aparelhos para essa tentativa; vídeo, CPU, sincronização e rede ainda não foram separados. A falha `N64_OWNER_MISSING` descrita abaixo pertence ao APK anterior e motivou a correção R159. Preservar a R159 como referência e preparar qualquer mudança de CPU em candidata isolada, com identidade própria; não promover a candidata nem alterar o relay remoto com base apenas no relato.

O restante do documento guarda o histórico da correção e o procedimento de recebimento já concluído pelo servidor. A R157 estável permanece como ponto de recuperação.

### Primeiro teste da tela preta, sem trocar o APK

O [mantenedor do Mupen64Plus AE](https://github.com/mupen64plus-ae/mupen64plus-ae/discussions/1165) relata tela preta no Netplay quando a orientação está travada e sugere `Display > Orientation > Auto`. No código da R159, `GlobalPrefs` usa `displayOrientation` padrão `0`, e `GameActivity.onGameStarted` aplica `setRequestedOrientation(0)` quando essa opção não é `-1`; a ponte Station não a altera. Isso estabelece uma hipótese específica, **não uma causa confirmada** para o 007, cujo áudio também engasga.

Com os dois aparelhos na R159 exata e a mesma edição do jogo, abrir **Engrenagem Station → Nintendo 64 → Configurações → Exibição → Orientação → Auto**, ativar a rotação automática do Android, iniciar uma sala nova e registrar o resultado. Anotar o valor anterior para poder restaurá-lo. Se a tela continuar preta, capturar simultaneamente nos dois telefones o início de `CoreService`, `onGameStarted`, Surface/EGL, primeiro quadro, estado do motor, chegada de entradas e logs de exceção; correlacionar no servidor WSS, bytes, READY e `hasPlayed`. Não mudar renderer, JIT ou prazo de espera antes dessa separação.

O [perfil online Station](../../../versions/station-mupen-online-r125-20261011/STATUS.md) fixa CPU `cached-interpreter` (`r4300Emulator=1`), vídeo Glide64mk2 e atraso de entrada 2. Os perfis offline incluídos no APK usam `dynarec` (`r4300Emulator=2`); a preferência salva da ROM pode modificar o perfil individual efetivo. O [core oficial](https://github.com/mupen64plus/mupen64plus-core/blob/master/doc/emuwiki-api-doc/Mupen64Plus-Core-Parameters.mediawiki) define 1 como intérprete com cache e 2 como recompilador dinâmico; seu [protocolo de netplay](https://github.com/mupen64plus/mupen64plus-core/blob/master/doc/emuwiki-api-doc/Mupen64Plus-v2.0-Netplay-API.mediawiki) sincroniza `emumode` entre anfitrião e convidados. A restrição a 1 está no adaptador Station: `StationN64Bridge.Session.prefs()` força 1 e `StationN64Server.tcp()` rejeita qualquer outro valor com `N64_CPU_PROFILE`. O servidor N64 desse trecho roda **no telefone anfitrião**; o servidor remoto autentica e transporta a sessão. Uma candidata com 2 requer alterar os dois pontos, as opções/fingerprints e cadastrar a nova identidade exata no servidor remoto, mantendo a R159 como retorno. Não misturar revisões na mesma sala. O caminho oficial admite 2, mas ainda é necessário comprovar sincronização e desempenho entre dois Androids antes de chamar a candidata de estável.

Há uma segunda fonte possível de engasgo: o adaptador reserva apenas dois eventos de entrada (`DELAY=2`, `BUFFER=2`) e transporta os datagramas Mupen por WebSocket/TCP até o anfitrião. Quando o atraso da rede supera essa margem, o core espera entradas reais e pode interromper imagem/áudio. Dois eventos não equivalem necessariamente a dois quadros; dependem do ritmo de leitura de controles do jogo. Medir RTT, jitter, VI/FPS e esperas antes de alterar esse valor; mudar apenas `BUFFER` sem `DELAY`, sementes e fingerprint não é correção válida. O conserto de CPU não substitui uma correção de latência caso as esperas continuem.

Em 15/10, a leitura USB do A56 confirmou que seu `base.apk` tem SHA-256 `4837be706f9ac1f913b4844353741f70c539674e125ae185239611cf5faac2b3`: a versão instalada é a R159 exata já cadastrada. No momento da leitura a tela estava bloqueada, sem logs recentes da partida; a observação do proprietário é que o mesmo jogo roda bem offline e engasga online. Esta diferença de comportamento é real, mas não separa CPU e espera de rede sem uma captura simultânea.

## Identidades finais e entrega

| Artefato | Identidade conferida |
| --- | --- |
| APK `TurboStations-Premium-R159-n64-owner-20261014.apk` | 2.715.106.319 bytes; SHA-256 `4837be706f9ac1f913b4844353741f70c539674e125ae185239611cf5faac2b3` |
| ZIP privado `Kit-Servidor-TurboStations-R159-Independentes.zip` | 12.187.843 bytes; SHA-256 `d1f0b3805392c13b597e85b4ef8ec1fa938c19f77f2e5f5ee6ee3f2948c5454c` |
| `assets/station-online/engines.json` no APK | SHA-256 `ba1b5306842559a4fe741eab16055c1f438d3a470b2bb04474a0e15d8d4260fe` |
| Certificado Android preservado | SHA-256 `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`; assinatura v2/v3 e alinhamento de 16 KiB conferidos |

O [recibo do APK](final-apk-proof-r159.json) confirma 13.855 das 13.860 entradas preservadas, incluindo 39 partições DEX, manifesto Android, versão interna e o visual R158. Mudaram somente `classes35.dex`, `engines.json` e os três marcadores ELF de PSX, N64 e Dolphin. O [recibo do kit](server-kit-receipt-r159.json) lista as quatro identidades novas, os seis ELF e as vinte dependências com hashes individuais; as 39 entradas do ZIP foram conferidas contra o APK assinado. O ZIP não contém APK, ROM, BIOS, chave, serial nem log bruto. O [recibo de instalação do g52](g52-install-receipt.json) confirma `adb install -r`, dados preservados e SHA-256 do `base.apk` instalado idêntico ao APK assinado. O servidor recebeu o kit exato; a entrada em gameplay N64 ainda não foi validada fisicamente após este reparo.

As identidades R159 são:

```text
pcsx-rearmed-ff81ed17-player-v3-7d5350589348
mupen64plus-ae-33bf7021-n64-v3-7d50f27b4702
dolphin-5102a033-gamecube-v3-008698beba99
dolphin-5102a033-wii-v3-008698beba99
```

## Falha observada e limite da prova

No moto g52, a lista de salas abriu. Ao iniciar o jogo N64 a partir de uma sala, o processo do emulador `:n64core` falhou em `CoreService.onStartCommand`. O trecho **filtrado** do diagnóstico, sem sala, aparelho, licença, token ou prova de requisição, é:

```text
Processo: :n64core
java.lang.IllegalStateException: N64_OWNER_MISSING
  at StationN64Bridge.onServiceStart(StationN64Bridge.java:33)
  at CoreService.onStartCommand(...)
```

Esse registro demonstra falha local na entrega do marcador de sessão ao serviço. Ele não prova que o jogo entrou em emulação, que dois controles funcionaram ou que uma partida física foi concluída. O recibo anterior de [criação de sala no Samsung](../r157/server-fingerprint-fix/physical-room-created.json) comprova uma etapa diferente: criação e confirmação da sala no servidor, sem gameplay físico.

## Motor usado e causa no aplicativo

O N64 online usa o **Mupen64Plus AE integrado**, não o RetroArch. `StationRoomsActivity` escolhe `StationN64Activity` quando o backend é `mupen64plus-ae-station-v1`; a Activity lança `paulscode.android.mupen64plusae.game.GameActivity`. `GameActivity` e `CoreService` executam em `:n64core`. Depois de consumir o descritor privado válido, `StationN64Bridge` seleciona `station_mupen64plus_core`; fora da sessão Station, mantém o core Mupen normal `mupen64plus-core`. Ver [StationRoomsActivity](../../../integration/psx-online/java/org/emulationstation/frontend/netplay/StationRoomsActivity.java), [StationN64Activity](../../../versions/station-mupen-online-r125-20261011/java/org/emulationstation/frontend/netplay/StationN64Activity.java) e [StationN64Bridge](../../../versions/station-mupen-online-r125-20261011/java/org/emulationstation/frontend/netplay/StationN64Bridge.java).

O `CoreFragment.actuallyStartCore(Activity)` do Mupen passa `Activity.getApplicationContext()` a `ActivityHelper.startCoreService`. O hook em [patch_mupen_smali.py](../../../versions/station-mupen-online-r125-20261011/recipes/patch_mupen_smali.py) chama `StationN64Bridge.propagate(context, serviceIntent)` antes de `startService`. Porém `isStationContext` reconhece somente a `GameActivity` exata ou um serviço **já registrado**. No primeiro start, o contexto recebido é o `ApplicationContext`, e o serviço ainda não foi registrado. `propagate` deixa o Intent sem `station.n64.launch`; `onServiceStart` recusa esse Intent com o `N64_OWNER_MISSING` do log. A composição R158 documentou a preservação das 40 partições DEX e, portanto, não corrigiu esse caminho ([prova de pacote](../../../integration/psx-cover-map-r158/evidence/final-apk-proof.json)).

O reparo no app deve propagar o marcador **somente** quando há uma sessão Station já validada, o contexto é o `ApplicationContext` da `GameActivity` dessa sessão e o Intent aponta explicitamente para o `CoreService` integrado. A propagação atual a partir do serviço já registrado, usada na notificação, deve continuar. `onServiceStart` precisa manter a recusa de marcador ausente ou diferente. Não ampliar `isStationContext` globalmente: ele também governa saves, preferências, desligamento e controles. O início offline continua sem marcador e com o Mupen normal. A alteração deve ser verificada no DEX real e no APK final; esta descrição não substitui o teste físico.

## Por que o servidor não causa este erro

`N64_OWNER_MISSING` é lançado dentro do processo Android antes de o `CoreService` obter a posse da sessão. O servidor não constrói o Intent Android nem escolhe o objeto `Context` usado por `ActivityHelper`. A falha não se resolve alterando capacidade, perfil, ROM, rede, licença ou proteção contra replay. O [retorno R157](../r157/server-return.json) registra motores e perfis N64 aplicados, capabilities assinadas, transporte público sintético e a criação física de uma sala no Samsung; essas provas não cobrem a abertura do serviço Mupen no moto g52. Depois do reparo do aplicativo, os **novos** fingerprints do APK terão de ser registrados no servidor por identidade exata.

## Verificações antes de entregar o kit

1. Conferir qual APK assinado será a base efetiva e os hashes `base.apk` dos aparelhos; há recibos de candidatos visuais R158 diferentes. Não inferir identidade pelo rótulo R158 nem aproveitar o kit de outra compilação.
2. Conferir o diff limitado do reparo, assinatura original, alinhamento, DEX/ELF/manifesto de motores e todas as entradas preservadas. Os hashes do APK, ZIP, `engines.json`, seis ELF de motor e vinte dependências reais constam dos recibos acima. A verificação dos bytes assinados está concluída; não substitui o teste físico.
3. Exercitar `GameActivity` e `CoreService` reais: primeiro start online deve levar o marcador ao Intent do serviço; marcador errado ou ausente deve continuar recusado; abertura offline deve usar o core original sem marcador. Conferir também retomada/notificação, saves isolados e saída humana sem regressão.
4. Instalar o APK final por cima, preservando dados, e verificar seu hash integral no telefone. Reproduzir sala N64 em dois aparelhos com o **mesmo APK, edição e perfil**; registrar passagem pelo serviço, READY, controles distintos, perda/retomada de sinal e saída. O teste local do serviço não deve ser apresentado como partida física concluída.

## Procedimento histórico de recebimento R159 — concluído pelo servidor

1. Receber o ZIP privado R159 e o recibo do APK final; comparar SHA-256 externo, manifesto interno, quatro `engineId`, seis ELF, vinte dependências e campos de motor com os bytes do APK assinado. Recusar kit R157/R158 ou um ZIP reconstruído a partir de outra candidata. Não publicar APK, ROM, BIOS, chaves, serial ou logs crus no Git.
2. Ler baseline **efetiva e fresca** de catálogo, motores, perfis e modos. Conciliar primeiro os recibos já aplicados de R151/R152, revisão completa N64, R157 e do APK `6203804f` registrado no [retorno atual](../r157/server-return.json); não reexecutar operadores históricos nem substituir registros anteriores.
3. Se a identidade R159 for nova, usar o registrador vigente com o kit final para adicionar as quatro identidades e os **78 modos revisados** da primeira transação. Após o primeiro recibo, obter outra baseline fresca e vincular ao motor N64 R159 as **321 decisões** já revisadas das 157 edições, pela [revisão completa](../n64-completo-20261014/README.md). Preservar jogos individuais sem vaga, `allowedPlayerCounts` por modo e o fingerprint canônico compatível com `JSONObject.quote` do Android. Executar sob `scan.lock`, backup e rollback do operador vigente; os totais efetivos virão dos recibos, não de uma soma presumida.
4. Verificar capabilities autenticadas e assinadas com `itemId`, conteúdo, `engineId`, core, runtime, `profileId` e `profileSha256` exatos da R159; provar admissões permitidas e recusas inseguras. Testar criação/entrada/transporte na rota pública com identidades R159 e limpar somente licenças sintéticas próprias. Registrar separadamente o teste de dois Android. Nenhuma mudança de DLL, scanner, catálogo de ROMs/capas ou regra de replay decorre deste defeito.

**Pendente:** conferir a R159 exata nos dois telefones e testar gameplay físico após o reparo. Não marcar esses testes como concluídos antes das respectivas provas.
