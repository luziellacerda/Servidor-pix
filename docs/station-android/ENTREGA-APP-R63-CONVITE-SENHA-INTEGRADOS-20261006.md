# R63 instalada nos dois aparelhos — recibo Android07/10/2026

Leia [RECIBO-APP-R63-DOIS-APARELHOS-20261007.md](RECIBO-APP-R63-DOIS-APARELHOS-20261007.md). APP → SERVIDOR. Samsung A56 e Motorola Edge30 atualizados, mesmo APKd9a35602 conferido integralmente; LoginActivity voltou ao catálogo sem nova licença. Fonte/documentação `8613d88d4f553e72e5fedcd1d3ea470010301734`. Falta de espaço Samsung resolvida; textos anteriores são históricos. Gameplay em dupla permanece pendente, conferências físicas a cargo do mantenedor. Criar sala nova com dois aplicativos R63; sem novo deploy Linux.

## Histórico preservado

# Entrega Android R63 ao Servidor-pix — 06/10/2026

**Direção: APP → SERVIDOR.** A resposta8d9c670 e a implementação1e0f862 foram integradas no APK sucessor da R62. Esta entrega não exige nova implantação Linux. Analise o app exato abaixo; não retome R41/R55/R57 isoladas.

- Branch app: `fix/station-r63-auto-access-20261006`.
- Commit completo: `c7ac337e31c9a5608407c5502c84781f1bb2df47`.
- [Handoff completo](https://github.com/luziellacerda/TurboElden/blob/c7ac337e31c9a5608407c5502c84781f1bb2df47/versions/station-auto-access-r63-20261006/HANDOFF-APP-R63-PARA-SERVIDOR-20261006.md).
- [Fontes, receitas e testes](https://github.com/luziellacerda/TurboElden/blob/c7ac337e31c9a5608407c5502c84781f1bb2df47/versions/station-auto-access-r63-20261006/README.md).
- APK R63 SHA256 `d9a35602ddc1141617df70d4b2b1f202d8646c538d4508f5fb3e53ef6b7abc4c`.
- DEX35 `e910f431b2f3cd3117389afb3e831a972f5284c38f683d6f06602770bd366b2e`.
- Runtime recebido `899e35279cf046242a7ae31f502078080bd6a94404770fe99e1e2e90be58a1ef`.
-302 testes Windows; restauração das161 fontes e DEX idêntico;40 arquivos desta entrega conferidos também nos blobs Git publicados.

Instalação R63 ainda aguarda saída segura do jogo no Motorola Edge30, última versão conferida R58. Samsung A56 R62 desconectado. O mantenedor fará conferências físicas. **Atualizar ambos para R63 e criar sala nova** antes de validar gameplay. Não há partida em dupla comprovada, estabilidade geral, medição nova de latência ou controles online próprios resolvidos. Sessões, emuladores locais, dados e certificado preservados.

Consumo do contrato: convite curto anunciado por capability, resolve-code autenticado, verificação de locator, preparação local, join confirmado; nenhuma senha em convite. Nativo somente envia senha assinada automaticamente no lançamento Station cliente, NICK antes de PASSWORD e verificador do host intacto. NeoGeo permanece indisponível no online. Não solicitamos mudança de cadastro/licenças, download, catálogo ou outros produtos.

## Cópia integral do handoff app, para leitura sem ambiguidade

# APP → SERVIDOR — R63 integrada: convite curto e senha automática

## Destinatário e finalidade

Ao operador/implementador do **Servidor-pix**. Esta é entrega **Android**, não novo retorno do servidor nem ordem de implantação Linux. Somente TurboStations. O mantenedor assumiu as conferências físicas. Não declaramos gameplay homologado em dois aparelhos.

Retorno consumido: [8d9c670ba813fb970a61ff9bf329e6286b51ad07](https://github.com/luziellacerda/Servidor-pix/blob/8d9c670ba813fb970a61ff9bf329e6286b51ad07/docs/station-android/RETORNO-CONVITE-CURTO-SENHA-AUTOMATICA-STATION-20261006.md), branch `fix/station-auto-room-access-20261006` e outros ramos indicados no próprio retorno. Implementação recebida TurboElden `1e0f86241851d6c9c17c379ed23fd238637bbcbd`, pasta `versions/station-auto-room-access-r57-20261006`; documentação subsequente `029612b06205ff66cc1c7dcb2a2dd1a4c47aa1f9`. Os34 arquivos recebidos foram extraídos e conferidos pelo manifesto.

Base conciliada **R62**, commit `087b6823814d1ec6fc3b925dba8045d9b5628f02`. Não restauramos a Activity antiga R57. Preservamos os componentes de prontidão/Binder/saída e o visual atual. São três Java alterados e 158 preservados, além de runtime/manifesto/licença.

## Identidade e caminhos

| Artefato | Identificação |
| --- | --- |
| Pacote | `org.turboramastation.frontend`; classes `org.emulationstation.frontend` |
| Fonte/build | `E:/ESTUDO APK/work/station-auto-access-r63-20261006` |
| APK | `G:/BAKUP SISTEMA APP 03-10-2026/apks-candidatos-visuais/TurboStations-Premium-R63-20261006.apk` |
| APK SHA256 | `d9a35602ddc1141617df70d4b2b1f202d8646c538d4508f5fb3e53ef6b7abc4c` |
| APK bytes | 2093321666 |
| DEX35 | `e910f431b2f3cd3117389afb3e831a972f5284c38f683d6f06602770bd366b2e`,389560 bytes |
| Runtime online | `899e35279cf046242a7ae31f502078080bd6a94404770fe99e1e2e90be58a1ef`,10666496 bytes |
| engines.json | `36504c3c8a5b3ec6d7fd5ce74b431d2534809e94d018babc944e560d2eee62fd` |
| Certificado | `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825` |

BUILD-RECEIPT.json contém a evidência mecânica. Nenhum APK, ROM, BIOS, chave privada, licença, convite real, serial ou log pessoal publicado.

## Código e contrato completos deste delta

**StationInvitationCode:** mantém TS1 e aceita oito caracteres do alfabeto `23456789ABCDEFGHJKLMNPQRSTUVWXYZ`, exibidos `7KPM-4XRT` (exemplo sintético). Aceita minúsculas, espaços externos e hífen central; valida comprimento/caracteres. É localizador público, não senha nem autorização.

**StationRoomsActivity:** exibe room.inviteCode se disponível. Só usa código curto com snapshot autenticado anunciando `roomCapabilities:["short-invite-v1"]`. Fluxo:

1. Parser valida entrada; exige conexão e ausência de outra sala.
2. `StationOnlineClient.command("resolve-code")` gera requestId novo; POST `/v1/station/online/command`, inviteCode e sessão/protocolo de assinatura existentes.
3. Extrai resolvedRoom da resposta autenticada e valida instance/roomId/itemId. Resolver não entra na sala nem expõe credenciais de membro.
4. Atualiza capa/nome do item resolvido na UI, somente se ativa e no mesmo epoch; usa cache/autenticação R62.
5. `StationOnlineGame.prepare` verifica arquivo local, motor e identidade da edição.
6. POST `join` com roomId e hashes de ROM/core/runtime/opções.
7. Exige sala com roomId correto e selfId membro; `game.verifyRoom` confirma a edição. Só depois define prepared e navega para Sua sala.

TS1 mantém caminho anterior e validação da instance. Erros usam feedback existente; não inventamos sala, não baixamos ROM automaticamente e não retiramos verificações. A resolução autenticada antecede qualquer preparação local.

**StationOnlineClient:** duas mensagens novas, `STATION_ONLINE_CODE_INVALID` e `STATION_ONLINE_CODE_NOT_FOUND`. Mantidos tratamentos de sala cheia/encerrada, bloqueio, sessão e incompatibilidade. Login/catálogo/capa/download intactos.

**Runtime nativo:** incorporado exatamente o .so recebido, aviso e licença. Upstream RetroArch `69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576`, patch Station original mais `retroarch-station-auto-password.patch`. Receita `recipes/build_native_runtime.py` fixa arquivo upstream por SHA e NDK r28c/API 26/ARM64/16KiB; sem Vulkan/Cheevos/SAF. Conferimos ELF, símbolos, alinhamento e hash no Windows; não alegamos recompilação nativa local.

Causa estabelecida no retorno: ao receber salt o cliente nativo abria diálogo manual embora a configuração já contivesse a senha privada da resposta assinada. Nova função permite senha automática somente no lançamento tipado Station como cliente. Valida 64 hexadecimais minúsculos; NICK precede PASSWORD; preserva salt/SHA256/verificador anfitrião. Credencial ausente/inválida falha, sem bypass. Outros lançamentos conservam o fluxo manual.

**Compatibilidade:** novos IDs `bsnes-mercury-performance-79d7f9de-autopass1` e `clownmdemu-d43c2708-autopass1`, runtime899e3527. Cores, opções e overlays iguais. `geolith-19402493-autopass1` continua launchReady=false. **Atualizar os dois celulares para R63 e criar sala nova.** R62/R58 têm outro runtime/engineId. Registro servidor mantém versões antigas aditivamente; não pedir remoção. Emuladores locais/controles próprios preservados; online continua RetroArch. Senha automática não resolve sozinha controles online próprios, latência ou aquecimento.

## Preservação e restauração

Composição: R55 → R57 → R62 → três fontes R63.161 entradas Java com hashes,158 iguais à R62. StationHostConnector/StationRelayTunnel/StationRetroActivity idênticos ao candidato recebido. Preservados painel sem célula interna, capas em todos os passos, miniaturas, botões finos, confirmação de criar sala, Binder, saída, faixa INSTALADO, carrossel nativo R57, catálogo e cache.

1. `python recipes/restore_sources.py E:/ESTUDO-APK-build-novo` restaura 161 fontes, testes/runtime/assets e receitas; exige diretório inexistente emE:.
2. Dentro da pasta restaurada: `python build_java.py final`, depois `python run_local_tests.py`.
3. Para assinatura, fornecer ambiente privado STATION_KEYSTORE, STATION_KEY_ALIAS, STATION_KS_PASS, STATION_KEY_PASS.
4. `python package_r63.py` exige APKbaseR62 SHA114dba8a, certificado original, espaçoE:/G: e destino inexistente. Ajustar apenas nome do candidato de saída se necessário; não sobrescrever entrega já feita.

Dependências externas: JDK17; Android34 SHA `6cea1df3efb77103ac3e2beb9bf4718964b0e0869ab16d39d29d5cbae1c147ad`; station-client.jar SHA `e41854977e9c2dab786f431449c95afb759e80c653e02cbc990434f74a2c39a2`; D8 SHA `d43c8a94c9b1f1da1a7cc49c32b81e8cee1708b37ee8b530a81a0688222b42c0`; JSONtest jar indicado na receita. C++/mídias privadas/baseAPK permanecem externos e mapeados emR55/R57/R62. Nenhum stub criado. Não executar empacotadorR57 antigo sobreR63.

Trocas APK: classes35.dex, runtime online, engines.json. Acréscimo AUTO-PASSWORD-NOTICE.txt. Todas as 13.191 demais entradas comparadas por SHA, incluindo resources.arsc/classes30.dex/carrossel nativo. Certificado/alinhamento16KiB passaram.

## Testes e limites

Executados neste Windows sobre classes finais que geraram DEX:107 de lobby, 20 de entrada, 15 de lançamento, 113 sociais, 23 de convite curto, 10 de TCP real e 14 de confirmação de criação = **302 verificações**. Compilação real Java 8/API 34/D8 min 26 de161 fontes. Evidence/local-tests.json identifica cada saída.

Evidência recebida Linux:66 verificações de handshake nativo extraído, transporte/menu sintéticos, e39 verificações de TCP/TLS/relay. Não repetidos nesta rodada no Windows. Os 454 testes Parcel/ResultReceiver Android da R59 continuam aplicáveis ao código preservado, não são execução nova da R63. Não há prova nova de gameplay em dupla.

## Produção e instalação

Segundo handoff do operador, sem inspeção Linux nesta execução: API `a3e83d96b8025cde058e267eaa74e4505f654b15`; DLL `5fff55c11a7e85d97d0ba155ea0387584dbeea207a82be294bda958655bf5331`; PID970425; publicação23h04 UTC; registro `a4412aa8139b865d62b1dc42cb4b08f7fc656b3a7efca6237df7854d2ef888cc`. Catálogo 14/2212 e relay 512/1024 preservados. Retorno registra183 verificações isoladas, 2554 de regressão do relay, 198 de HTTPS público e 65539 bytes/direção. Não equivalem a gameplay Android. Nenhum deploy/migration/mensagem externa por esta execução.

Na preparação, Motorola Edge30 estava em StationRetroActivity. Foi solicitada saída pelo próprio menu; última versão conferida nele R58. Samsung A56 R62 desconectado. **R63 aguarda instalação segura**; consulte STATUS e futuros recibos. Não encerramos partida, desinstalamos, limpamos dados ou trocamos assinatura. Mantenedor faz conferências físicas. Sem estabilidade geral alegada.

Após instalar R63 em ambos: sala nova, Battletoads mesma edição; convite curto → resolve → join; dois nomes, ambos Pronto, Iniciar; TCP/WSS/host-listening/connecting; ausência de diálogo de senha; frames/inputs de ambos; saída HUD/Voltar e retorno. Correlacionar privadamente UTC,roomId,generation,engineId,papel, sem publicar segredo/ticket/licença/serial. Testar cancelamento/perda de rede separadamente. Primeira divergência deve ser comprovada, nunca forçar connecting/desativar autenticação.

Pendências reais: instalação após saída da partida; atualizar segundo telefone; gameplay/retorno em dupla; controles online próprios/latência externa. Contrato e código integrados. Não há nova rota ou alteração Linux pedida nesta entrega.

## Reprodução da entrega publicada

As fontes foram restauradas pelas receitas desta pasta em `E:/ESTUDO APK/work/station-r63-git-restore-20261006`. Os161 hashes de entrada coincidem e o DEX recompilado é byte a byte idêntico ao incorporado no APK. Os302 testes foram repetidos com sucesso nessa restauração. Recibo: evidence/git-restore-rebuild.json.
