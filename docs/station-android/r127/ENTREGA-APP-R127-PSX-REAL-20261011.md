# R127 — PlayStation com o emulador normal: entrega APP → SERVIDOR

Esta é a entrega do app, não uma resposta do servidor nem confirmação de implantação. Fonte executável [50eefa3eb40f0987b9dc6001e1d10ed2ad81a77c](https://github.com/luziellacerda/TurboElden/commit/50eefa3eb40f0987b9dc6001e1d10ed2ad81a77c), branch `fix/station-psx-real-owner-r127-20261011`, diretório `integration/psx-online`. O APK R127 está compilado, assinado e integralmente verificado: SHA256 `a350878cca5c1a65c51e79ac13e9e38c2053d76e5404edb515ad1ddd650c3038`, 2.235.617.465 bytes. Não foi instalado; cadastro efetivo e gameplay físico não foram confirmados.

## Ação concreta do operador

1. Acrescentar os **quatro registros exatos** de `server-engine-additions-r127.json` ao registro efetivamente usado por `Station:Online:EngineRegistryFile`, preservando as entradas dos clientes anteriores. O arquivo entregue é uma lista de adições, não substituto do registro completo. Conferir limite total de 32 entradas e 32 KiB.
2. Para N64/GameCube/Wii, acrescentar perfis com os novos `engineId/runtimeSha256`, conservando os hashes de conteúdo, core, perfil, opções, modos e capacidades já aprovados na entrega R126. Os marcadores mudaram porque DEX/manifesto compartilhados receberam a integração PSX; cores, plugins, controles e políticas N64/Dolphin não mudaram. Preservar também os perfis das identidades anteriores para seus clientes.
3. Para PSX, aprovar individualmente a edição e o modo **simultâneo de dois jogadores**, usando o core normal e a política descrita abaixo. Não migrar em massa aprovações do PSX RetroArch anterior: core, frontend, opções e identidade mudaram. Usar `maximumPlayers:2`, `allowedPlayerCounts:[2]`, `controllerProfile:"standard-2p-v1"`, e o hash canônico abaixo. `itemId`, `contentSha256`, `profileId`, `mode` e fontes da capacidade pertencem ao catálogo real do servidor; nenhum valor fictício foi incluído na entrega.
4. Conferir o serviço/DLL/configuração em execução e coordenar a recarga pelo procedimento do operador, com partidas preservadas. `AddStationOnline` lê o registro uma vez e captura o singleton: editar JSON sozinho não comprova ativação. O monitor de perfis não substitui a recarga do registro de engines.
5. Retornar evidência agregada da versão/PID/horário efetivos e de resposta autenticada fresca com perfis exatos para o app R127. Só então testar uma **sala nova** com dois clientes R127. Não usar cache, HTTP 200 isolado ou PONG como prova de identidade ou gameplay.

## Formato do registro e protocolo

O código comparado é `141b488d6ad7fd3d2c8e74e76c1a004f1cb257fc`, arquivos `src/TurboRamaSuiteOnlineServer/StationOnlineRegistration.cs`, `StationOnline.cs`, `StationMultiplayer.cs` e `StationMultiplayerProfileMonitor.cs`.

`OnlineEngine` usa `id`, `platform`, `coreSha256`, `runtimeSha256` e `recoveryProtocol` opcional. As adições entregues contêm somente os quatro primeiros campos. **Não inserir `recoveryProtocol:"station-stream.v3"` nesse registro v2:** a validação de `AddStationOnline` aceita nesse campo somente null ou `station-stream.v2`. O v3 desta entrega é a rota independente `StationMultiplayer.Protocol`, com capacidades/perfis de `/v1/station/online/multiplayer/command` e relay `/v1/station/online/multiplayer/relay`. O asset Android possui `recoveryProtocol:v3` para selecionar essa rota; os dois formatos têm funções diferentes.

O servidor transporta DATA TSR3 opaco. PXL1 é o envelope entre os clientes e não requer que o servidor execute PCSX ou interprete controles do PS1. Manter autorização, epoch/serial, PAUSE/RESUME, janelas, ACK/replay, orçamento e isolamento existentes. Nenhuma redução de validação, troca de identidade antiga ou mudança de segurança integra esta entrega.

## Identidades finais

| Plataforma | Engine | Core SHA256 | Runtime SHA256 |
| --- | --- | --- | --- |
| PSX | `pcsx-rearmed-ff81ed17-player-v3-f9d762e9fe82` | `e7534aa6f4a993e03f4563152f0e017324b710f5a5c224b0f1f722abeabed2b9` | `f9d762e9fe82b5346b65d29d356d6cb93776a983eb4bf9a9e25ab73388b1eb4f` |
| N64 | `mupen64plus-ae-33bf7021-n64-v3-9536fbd2a7d2` | `fe6e810a1c7fc50a494b50b1dccf581b84385667191c0cbcd235d585a0703dd8` | `9536fbd2a7d23713d1280a22276856924420dea9920bb557e0dda7117e5b3025` |
| GameCube | `dolphin-5102a033-gamecube-v3-a206aed4b7bb` | `f7ebe048e030a029c0667cf071ab4a49d702f77daf50d40ad40ed1f493ded37d` | `a206aed4b7bb491fb010deefde537742b1c95b0f7779f53a25a7a75e0edaf787` |
| Wii | `dolphin-5102a033-wii-v3-a206aed4b7bb` | `f7ebe048e030a029c0667cf071ab4a49d702f77daf50d40ad40ed1f493ded37d` | `a206aed4b7bb491fb010deefde537742b1c95b0f7779f53a25a7a75e0edaf787` |

Os hashes intermediários de compilação e os IDs R125/R126 não identificam este APK. Os registros antigos continuam válidos somente para seus clientes próprios, não para aprovação da R127.

## Política PSX exata

- Backend `psx-player-station-v1`, Android 9/API28 ou posterior, ARM64. Owner **LibretroPlayer normal** em `libmain.so`, SHA256 `62ad07ba8e62e3227d488f9075eb15167c2e95b721c43e480e2535b4319467f6`; core normal PCSX-ReARMed `ff81ed17a15241d2f3730cdd7585b38e172532ca`. A rota privada não usa StationRetroActivity.
- Perfil canônico em `psx-controller-profile.json`: bytes UTF-8 exatos, sem BOM nem LF final; SHA256 **`81c0e2323a37130dbdbdb9c2efefca8837a9bb6e7169304b4e5731b2ec56290a`**. Opções exatas em `runtime-final.json`: SHA256 **`8c1e81460dc19b6e8d151ec6231c9092000b4946e6f084222cb40d3aab2e66a9`**. `profileId` é o identificador do modo/edição real; seu `profileSha256` deve ser este hash canônico.
- HLE explicitamente aprovado por jogo; duas portas Standard, dispositivos `[1,1]`, sem multitap/analógico/light gun. DRC normal habilitado; threads DRC/GPU/SPU, frameskip e combinação analógica desativados. Atraso de entrada fixo de quatro quadros; saves novos privados da sessão. Não usar outra BIOS, save offline ou preferência local como opção silenciosa da partida.
- Formatos CHD/PBP: hash SHA256 do arquivo de lançamento real. CUE: `contentIdentityScheme:"cue-set-v1"`, incluindo o CUE e todas as faixas referidas. O catálogo assinado e os perfis precisam usar essa mesma identidade; hash somente do CUE é insuficiente. A implementação exata está em `versions/station-video-room-footer-r124-20261011/java/netplay-src/org/emulationstation/frontend/netplay/StationContentIdentity.java` do commit do app.
- Vínculo PXL1 inclui sala, geração, engine/core/owner/runtime, conteúdo, opções, perfil, esquema de conteúdo e máscara 3. Só o owner SDL executa o core. ACK de transporte/cópia e COMMIT de quadro real são separados. A pausa exige epoch/serial e checkpoint reais antes de estado/retomada. Perda de conexão espera recuperação; encerramento requer confirmação humana.

## Provas e limites

Verificados no APK: sete substituições e uma adição fechadas, **13.549 entradas preservadas**, mesmos emuladores offline, mídias, LEDs, cache/preload/economia, UI e SNES/Mega Drive. Assinatura v2/v3 original e alinhamento 16 KiB. `package-final.json` e `runtime-final.json` são os recibos finais.

Provas isoladas: 202.798 verificações do protocolo, 16.090 do owner/513 quadros simulados, 61 Session/Launch, 186 endpoint PSX, 470 regressões Dolphin/TCP, 80 ABI e 24 saída humana. Não são partidas em dois Android. O ensaio isolado D4 sustentou 60 Hz com RTT50 ms; RTT150 ms reduziu a progressão para 30 Hz. Não prometer ausência de delay ou desempenho constante em redes mais lentas.

Fontes originais exatas do LibretroPlayer não foram recuperadas na pesquisa local/GitHub. A integração usa o binário normal existente com guardas de hash/ABI e quatro entradas GOT em memória somente no processo privado, preservando o arquivo normal. Compatibilidade física de estado, áudio/vídeo, Bluetooth, retomada, saída e consumo ainda exigem os dois aparelhos; BIOS HLE não tem aprovação universal.

Esta entrega contém apenas código referenciado, JSON e recibos públicos. Não inclui APK, ROM, BIOS, credencial, licença, serial ou logs pessoais; não implanta nem reinicia serviços.
