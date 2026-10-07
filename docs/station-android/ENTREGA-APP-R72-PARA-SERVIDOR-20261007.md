# Entrega R72 — APP → SERVIDOR Station Android

Código do app: `ca5126ed339463d8a5ae7cf79e1afe47ecb2c653`, branch `fix/station-r72-native-registration-profiles-20261007`.
[Fontes, receitas, testes e estado por aparelho](https://github.com/luziellacerda/TurboElden/tree/ca5126ed339463d8a5ae7cf79e1afe47ecb2c653/versions/station-native-registration-r72-20261007).
Resposta integrada: `c1e44a1225a101478ceb29f4e624885331872c0d`. Documento do cliente, não implantação Linux.

O operador deve analisar esta composição exata; não substituir fontes por Activities antigas.
As evidências anexas são cópias do commit acima, listadas em DELIVERY-FILES.json.
Os caminhos evidence/ do texto abaixo correspondem a entrega-app-r72-20261007/evidence/ nesta entrega.
Nenhum novo ID de motor ou deploy Linux é solicitado. Conferir diagnóstico físico e pendências abaixo.

---

# APP → SERVIDOR: R72, registro nativo e nomes da sala

Destinatário: operador do **Station Android no Servidor-pix**. Esta é a entrega do cliente em resposta a `c1e44a1225a101478ceb29f4e624885331872c0d`, branch `fix/station-r71-server-recovery-20261007`. Não é recibo de uma nova implantação Linux. Não alterar outros produtos.

## Identidades e reprodução

- App base: `f64f685d9e88697c7980bfd4e0663df142b9334c`, R71 completa.
- App novo: branch `fix/station-r72-native-registration-profiles-20261007`; o commit completo consta da entrega publicada junto a este documento.
- Pasta fonte: `versions/station-native-registration-r72-20261007/`.
- Build: `E:\ESTUDO APK\work\station-server-return-r72-20261007-build`.
- APK: `G:\BAKUP SISTEMA APP 03-10-2026\apks-candidatos-visuais\TurboStations-Premium-R72-20261007.apk`.
- SHA-256 APK: `a8d3d28bb70618c52debf6e4cb0acaaf1f3bd1de19fe410dda85f6953caaec8e`; 2122892378 bytes.
- Certificado preservado: `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`.
- DEX35 novo: `0907b8de6a7ff2653a6e8cdc53a62bbdeec1a04abda709f3ab22b90abfc1b484`.
- DEX28 idêntico: `1a04582a02804ecbe4b075dd487118a171b0b0f305dbb8884015bbede09b25d7`.
- Runtime idêntico: `d66267cd42507388f86034deb47e9f9670875784efb9afabfb8ffc64e3f3a856`.
- Registro de motores no APK idêntico: `034e02d866a5b5849ce093ed1b71748c3237b1c9ba93660aef1a70e7891bc6ac`.

As receitas restauram os 198 Java da R71 exata e sobrepõem dois arquivos. 196 são preservados. Compilar com `recipes/build_candidate.py`, verificar com `recipes/run_tests.py`, empacotar com `recipes/package_r72.py`. As receitas declaram seus argumentos e dependências; precisam da base privada e da assinatura local, ausentes do Git. Não substituir por receitas antigas. `JAVA-OVERLAY-MANIFEST.json`, `evidence/lineage.json`, recibos e `SOURCE-FILES.json` vinculam fontes e resultado.

## 1. O que foi integrado

### Registro JNI

`StationRetroActivity.java` corresponde exatamente ao candidato proposto pelo servidor, SHA `2c160af62ca00098a0a01a3eee266e8148bdc29d88e24de567468c2cc2e65fd8`.

Após `super.onCreate(state)`, quando existe recuperação, chama `System.loadLibrary("station_retroarch")`, verifica `stationRecoveryStatus()` e `stationRecoveryStalled()` e registra `game stage=native-hooks-loaded`. O registro ocorre antes de `nativeLoaded=true`. O tratamento de `LinkageError` em nativeControl registra a classe da exceção antes de comunicar `NATIVE_HOOK`. Não alteramos o motor, seu protocolo ou seus IDs.

### Nomes dos dois participantes

`StationRoomRoster` recebe o snapshot já verificado pelo cliente de segurança. Usa `room.memberProfiles` apenas sob a capacidade assinada `own-room-member-profiles-v1`, para sala própria que contém o usuário em `members`. Relaciona `{peerId,nickname}` por ID real, independentemente da ordem ou paginação de `peers`. Nomes da sala prevalecem sobre a página social. Campos inválidos, IDs duplicados e perfis externos não adicionam membros nem mudam anfitrião/prontidão. Revisões antigas não sobrescrevem nomes; mudança de instância limpa cache. Sem a capacidade, preserva o comportamento anterior.

## 2. Estado do servidor recebido

O retorno informa produção publicada às 21:59:53 UTC de 07/10: fonte `ab192bf1585e30f303d041f13b36a1f9c96d2caa`, DLL `815fc8bc99a9d16247488a1797d2726b928eb3e592fd8a19700371e8d57c3243`, registro `310fefece80c336840882d0c91235b765d59f160246df947393802ea0211d289`. Recuperação habilitada; `station-stream.v2` e `relay-wss-v2`; seis motores, incluindo os dois Windows rs2/d662.

**Nenhum novo ID de motor, migration, desligamento de segurança ou implantação Linux é solicitado por esta R72.** A mudança JNI e os nomes usam o contrato publicado. Preserve clientes v1, catálogo, chaves, licenças e os motores registrados. Se houver política adicional por hash do APK, relate sua configuração efetiva antes de alterá-la; não presumir bloqueio.

## 3. Falha observada e limites

O mantenedor relata que qualquer aparelho como anfitrião fica preto. O servidor registrou início aceito às 22:00:59.012, ticket host às .199, `recovery-failed` às .991078 e WSS 401 às .992762. A falha terminal veio antes do 401; não atribuir automaticamente a credencial ou rede. Nenhum host-listening/stream/convidado foi comprovado nesse recorte.

A coleta USB anterior ao patch não retornou linhas úteis dos tags. Portanto **não está comprovado no aparelho que NATIVE_HOOK foi a primeira causa**, nem que a R72 resolve toda a tela preta. O patch corrige uma omissão objetiva e aplica exatamente o candidato recebido. 1181 verificações locais passaram, mas não executam o carregador JNI Android nem uma partida real.

## 4. Pedido preciso ao servidor e conferência conjunta

### Evidência posterior à instalação (22:38 UTC)

Em tentativa com **Samsung R72 anfitrião e Motorola ainda R71 convidado**, o Samsung registrou `native-hooks-loaded`, `native-listening role=host`, `host-listening-ack`, STATE epoch1/state0 e PONGs continuados (amostras105–171ms). A imagem permaneceu preta. Portanto a R72 ultrapassou o registro JNI e a confirmação de escuta; ainda não houve partida demonstrada. No Motorola houve prepare/ticket recebido/launch Activity, mas sem novas linhas nativas; Android reutilizou processo de emulação anterior e registrou pause/stop timeouts. A captura ficou vazia. Erros de06/10 no crash buffer são históricos e não explicam automaticamente esta tentativa.

O mantenedor autorizou encerrar a tentativa travada e atualizar o Motorola. Não atribuir a espera restante ao servidor sem conferir os dois aparelhos na R72 e uma sessão nova. Evidência saneada: `evidence/physical-attempt-mixed-r72-r71.json`. Logs/imagens pessoais ficam somente no PC. A próxima resposta precisa distinguir falha JNI inicial, reutilização de processo e sincronização de protocolo; são etapas diferentes.

**Motorola atualizado para R72 às22:44:21UTC**, APK integral conferido e UID/data original preservados. Samsung também R72. Depois, Samsung chegou a ESActivity e Motorola a StationRoomsActivity. O fechamento do teste anterior foi autorizado pelo mantenedor para a atualização; não contar essa ação humana como nova queda espontânea. Os recibos de instalação dos dois estão anexos. Ainda é necessária a conferência de uma sala nova R72/R72.

1. Ler os dois arquivos exatos da R72 e confirmar a integração do candidato JNI e de memberProfiles. Não responder sobre Activities da R57 ou R71 antigas.
2. Para a próxima sala nova, correlacionar por IDs privados e horário: start aceito, ticket host/guest, host-listening, WSS de cada lado, primeiro motivo de encerramento e ordem causal. Publicar somente evidências saneadas, sem tokens/licenças/dados pessoais.
3. Conferir o perfil assinado de ambos os membros, incluindo quando não aparecem na página social. Comparar nomes exibidos por ID, sem inventar participante ausente.
4. Confirmar a produção efetiva ainda corresponde ao recibo acima; devolver mudanças posteriores de fonte/DLL/registro/capacidades, se existirem.
5. Após instalar R72 nos dois aparelhos, verificar Battletoads com Samsung anfitrião e Motorola convidado, depois inverter os papéis. Registrar imagem, áudio, comandos, nomes, saída para plataformas e recuperação de interrupção temporária de rede separadamente. Não declarar esses testes realizados apenas por esta entrega.

O mantenedor faz as ações físicas. A instalação preserva assinatura, UID/data original e dados, usando transferência direta. O estado de cada telefone está em STATUS.json e evidence/installation*.json; não assumir que ambos foram atualizados só porque o APK está pronto.

## 5. Preservações e rastreio

Somente classes35.dex foi trocado, sem adicionar/remover entradas; 13224 entradas preservadas, alinhamento16KiB, classes verificadas sem duplicação. Todos os demais DEX, 58 vídeos, BIOS, motores, controles, carrossel, faixa INSTALADO e limite de30fps somente no menu permanecem idênticos à R71. Nenhuma medição térmica nova. Nenhum APK, ROM, BIOS, segredo ou log pessoal publicado.

O material recebido está em `server/`, vinculado a caminhos/commit/hashes em `server/ORIGIN.json`. `evidence/package.json` identifica o binário; `evidence/local-tests.json` identifica as 1181 verificações; `evidence/java-dex-build.json` identifica a composição compilada. Este documento encerra a integração do retorno recebido, não a homologação física do netplay.
