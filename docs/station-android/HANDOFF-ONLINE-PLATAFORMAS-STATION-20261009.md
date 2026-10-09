# Station: servidor concluído para todas as plataformas; próxima etapa é o app — 09/10/2026

## Resultado efetivo

**Atualização aplicada e conferida pelo domínio público.** DLL `34fdc4b1ecea3216b3d30e49e003ef32a0520ca36637d186e9daeb369146645d`, fonte `eb826d5df17dad51314d265e2cba91b05d1c96cf`, PID **1820513**, NRestarts **0**, última recarga `Fri 2026-10-09 14:57:04 -03` e conclusão `2026-10-09T18:09:52.413592+00:00`. Catálogo **revisão 27 / 3.848 IDs / 3.593 visíveis / 255 aliases**, identidade de conteúdo de todos os 3.848 itens e **5.176 perfis**, preservando exatamente os **3.672 anteriores**. Foram acrescentados 1.504 perfis de oito plataformas. Aprovação de uso não declara gameplay Android homologado.

O servidor está publicado para SNES, Mega Drive, N64, Neo Geo, Neo Geo CD, PlayStation, FBNeo, CPS1, CPS2 e CPS3, nos modos e formatos cadastrados. Dreamcast, GameCube, Wii, Wii U e Switch também têm o servidor concluído, com transporte, cadastro e modos preparados. A produção do APK implementará seus motores na próxima etapa. Switch possui somente Pokémon Café Mix no catálogo, sem modo local de duas pessoas confirmado. **Servidor concluído para todas as plataformas solicitadas. Jogabilidade nos celulares será conferida após a integração do app.**

Backup anterior publicado antes das alterações, em ambos os repositórios: branch `backup/station-online-antes-todas-plataformas-20261009`, tag anotada `backup-station-online-20261009-antes-todas-plataformas`; servidor `2b04f591eb10ad76efc3b630ded4fff9ef2c2a27`, app `4521218492ec65c33398aed07893f95e2d7f499d`. Branches atuais: `feat/station-online-all-platforms-20261009` no servidor e `feat/station-online-all-platforms-client-20261009` no app. Código cliente compilado em `cc74549`; use a pasta atual desta entrega para seus recibos posteriores.

## Ordem atendida: concluir o servidor antes do app

**O lado do servidor está preparado para todas as 15 plataformas normalizadas e os 17 rótulos do catálogo.** A implementação dos motores no app é a próxima etapa da produção do APK. Não é necessário esperar essa implementação para concluir o servidor.

Além dos vínculos já publicados, o servidor agora tem os **273 jogos restantes cruzados com itemId, conteúdo e coverId em 274 modos preparados**. Há nove modos com até quatro vagas. Dreamcast, GameCube, Wii e Wii U têm teto de quatro; Switch tem teto de dois. Cada modo continua respeitando suas entradas humanas reais. GBA Link, controles compartilhados e Co-Star usam layouts próprios: um motor com quatro gamepads comuns não recebe esses modos por engano.

O arquivo `server-prepared-modes.json` contém esses dados sem inventar hashes de motores. O manifesto protegido de motores do app é persistente e o importador consome ambos. Quando o APK implementar um motor, seu manifesto com **hashes dos binários reais, formatos e configurações dos controles** será cadastrado pelo operador local já entregue. Ele verifica os dois binários e cria os vínculos exatos de conteúdo/motor/runtime/layout. A API os recarrega em até dez segundos. **Esse cadastro não exige recompilar nem reiniciar o servidor.** Os 5.176 perfis anteriores continuam intactos.

### Leitura do estado pelo aplicativo

A resposta assinada de `capabilities`/`snapshot` em `POST /v1/station/online/multiplayer/command` agora contém `serverPlatforms`, com todas as plataformas e seus tetos. Para o jogo selecionado, `platformPolicy` contém:

- `serverReady`: transporte e cadastro do servidor implementados para a plataforma.
- `transportProtocol`: `station-stream.v3`; `transportTopology`: `host-star`.
- `onlineAvailable`: há perfil aprovado com o conteúdo, motor, runtime e controles exatos deste jogo.
- `availability`: estado daquele vínculo. `online-engine-pending` com `serverReady:true` indica que ainda falta o manifesto/integração do motor do app.
- `maximumPlayers`: teto da plataforma. A quantidade da sala deve constar em `allowedPlayerCounts` do perfil escolhido.

A produção do APK deve implementar o adaptador nativo, usar os tickets automáticos, conectar um link duplex anfitrião↔cada convidado e respeitar HELLO, PAUSED, READY, ACK e a época global. O servidor encaminha o fluxo sem emular o jogo. Bytes e offsets de TSR3 são definidos no código/contrato entregue; um ACK confirma escrita no motor, e não somente recebimento na rede. O adaptador deve manter a sessão nativa durante a substituição de WSS.

### Cadastro de um motor entregue pela produção do APK

O manifesto novo deve declarar `schemaVersion:1`, `recoveryProtocol:"station-stream.v3"`, `launchReady:true`, identificador imutável, plataforma, extensões, máximo, `library`, `runtimeLibrary`, hashes SHA-256 reais e `controllerProfiles`. Cada layout fornece `controllerProfile`, `maximumPlayers` e `configuration` com `schemaVersion`, `controllerProfile`, `devices`, `coreOptions`. As configurações podem descrever dispositivos nativos; os nomes GBA/Co-Star correspondem a recursos que o motor precisa implementar.

A receita de `profileSha256` é SHA-256 de JSON compacto UTF-8 sem BOM/quebra final, Unicode sem escapes, campos na ordem `schemaVersion`, `controllerProfile`, `devices`, `coreOptions`. O manifesto precisa conter exatamente a configuração usada pelo APK. Um exemplo de formato, sem afirmação de teste nativo, está em `server-integration-contract.json`.

Executar `cadastrar-motor-online-station.py install` na release ativa, com `--config` apontando para a configuração privada efetiva, `--incoming` para o manifesto recebido, `--artifacts` para os binários dessa entrega e `--output` para uma pasta privada nova. A administração local do Linux é obrigatória; licenças/códigos do app não podem cadastrar motores pela API. O operador preserva a configuração dos outros produtos, guarda cópia dos registros anteriores e rejeita IDs reaproveitados com outros binários, hashes divergentes, modos acima do teto e conteúdo substituído.

`prepare` permite revisão offline do mesmo resultado sem publicar; `apply` publica uma candidata ainda correspondente ao catálogo atual. Para `install`, uma única autenticação Linux faz as duas etapas. O procedimento e suas verificações já estão instalados; a próxima entrega do app precisa fornecer seus dados reais.

### Verificação das plataformas restantes

Passaram **1.250 verificações C#**, **172 TLS loopback** e **oito testes Python de importação/cadastro**. As novas provas abrangem duas/três/quatro pessoas conforme o teto, início global, dados exatos em ambos os sentidos, desconexão, recarga de perfis e retomada. Uma instância isolada com hashes explicitamente sintéticos verifica também o transporte autenticado das cinco plataformas restantes. Esses motores de teste não são instalados no registro da produção. Recibo efetivo: `server-ready-deployment.json`.

A preparação é completa no lado do servidor. Os testes de emulação, controles, sincronização e desempenho dos motores reais nos aparelhos pertencem à próxima integração do app. O jogo Switch atualmente presente é Pokémon Café Mix, individual; o teto de dois da plataforma não cria outro jogador nessa edição.

## Plataformas e vagas reais

| Plataforma | Teto do servidor | Cadastro e requisito do app |
|---|---:|---|
| SNES / BR | 5 | Perfis anteriores preservados; cinco somente nos modos cadastrados e com runtime novo. |
| Mega Drive / BR | 2 | Dois; controles/portas do pacote anterior preservados. |
| N64 | 4 | Motor e quatro portas compilados; quatro modos documentados cadastrados. Outros modos ficam em uma ou duas vagas. |
| Neo Geo / FBNeo / CPS1/2/3 | 2 | FBNeo ARM64 compilado; quantidade limitada pelo driver exato e pelo cadastro individual. Seis conjuntos sem driver exato permanecem sem sala online. |
| Neo Geo CD | 2 | NeoCD compilado e estado entre processos corrigido. HLE não comprova compatibilidade de toda a coleção. |
| PlayStation 1 | 2 | PCSX compilado; PBP, CHD e três CUE com identidade de todas as faixas. Somente modos de controles distintos cadastrados; Worms Armageddon aguarda passagem de controle por turnos. |
| Dreamcast | 4 | Servidor pronto para quatro. O app implementará o adaptador Flycast; o GGPO atual contém duas entradas. |
| GameCube / Wii | 4 | Servidor pronto para quatro. Próxima etapa do app: ligar a sala Station ao ciclo nativo Dolphin/ENet. |
| Wii U | 4 | Servidor pronto para quatro; conjunto completo e modos preparados. O app implementará seu adaptador nativo. |
| Switch | 2 | Servidor pronto para dois. O único jogo atual é individual; a produção do app implementará um motor/modo compatível quando houver. |

Os tetos não concedem controles em campanhas individuais. Salas têm `allowedPlayerCounts`, `modeTitle`, `instructions`, perfil de portas e hashes exatos. `Ver detalhes` consulta a sala, sem assistir ao vídeo nem ganhar controle. Os modos N64 documentados são Bomberman 64 Batalha (2–4), Mario Kart 64 VS/Battle (2–4; GP no máximo 2), F-Zero X VS Battle (2–4) e Mario Tennis Exhibition em duplas (2 ou 4 no cadastro). SNES mantém os modos de Bomberman 1/2/3 anteriores; Bomberman 4/5 ainda não são ROMs presentes neste catálogo.

## Como o app deve consumir a produção

1. Usar o domínio `https://app.lzgames.com.br`, login/licença original, bearer e provas vinculadas ao aparelho. Contratos v1/v2, dez engines anteriores e flags de admissão permanecem preservados.
2. Ler `GET /v1/station/catalog?metadata=1` autenticado e validar a resposta assinada. A revisão atual é 27. Usar `itemId`, `platform`, `revision`, `coverId`, `artifact`, `metadata` e `contentSha256` recebidos; não construir nomes/IDs a partir de capas ou pastas.
3. Pedir capas em `GET /v1/station/covers/{coverId}`, usando o `coverId` daquele item. Manter os quatro workers já implementados. Sinopses vêm de `metadata.description`; lacunas podem ser preenchidas por dados do servidor.
4. Autorizar em `POST /v1/station/downloads/authorize` e consumir a URL/grant retornados em `GET /v1/station/artifacts/{grantId}`. Seguir o descritor de arquivo/pacote e seu `launchPath` completo. CUE acompanha suas faixas; Wii U acompanha `code/content/meta`. Nenhum throttling, espera ou verificação nova foi acrescentado ao downloader.
5. Para salas v3, usar `POST /v1/station/online/multiplayer/command` com as provas existentes. Consultar `serverPlatforms`, `platformPolicy.serverReady`, `onlineAvailable`, `availability`, `maximumPlayers` e os perfis assinados. Estados: `available`, `single-player`, `mode-pending`, `content-identity-pending`, `online-engine-pending`, `select-game`.
6. Escolher o perfil exato de conteúdo/motor/runtime/controles e um valor de `allowedPlayerCounts`. Executar criar/entrar/pronto/iniciar e usar os tickets retornados em WSS `/v1/station/online/multiplayer/relay`. Os convites e tickets são tratados pelo app; não pedir que o jogador digite um segredo extenso.
7. `contentIdentityScheme` é opcional. Sem ele, permanece o hash do payload de lançamento já usado. Com `cue-set-v1`, `StationContentIdentity.java` calcula a identidade do CUE e das faixas **somente ao preparar a partida online**. `wiiu-set-v1` identifica todo o conjunto `code/content/meta`, para o futuro motor Wii U.

## Importação automática e persistência

O timer do importador continua ativo. Após duas observações de arquivo estável, um jogo novo entra com ID estável, capa relacionada, descritor e identidade offline. O importador mantém o registro persistente junto do índice e publica os perfis por dados antes de publicar o catálogo. A API recarrega o registro a cada dez segundos, sem reiniciar nem descartar salas em andamento; arquivo parcial/inválido mantém o último registro válido. A correção final preserva o objeto imutável de cada perfil sem alteração, evitando que arrays desserializados façam uma sala existente recusar entrada, tickets ou reconexão. Os testes também confirmam que uma revogação real continua sendo recusada. Recibo: `profile-reload-fix.json`.

Política expressamente autorizada pelo mantenedor: jogos compatíveis recém-importados recebem até duas vagas, respeitando indicação individual e motor único disponível. Arcade novo com driver ainda não associado fica reconhecido no catálogo e aguarda associação por dados. Acima de duas pessoas exige modo/portas específicos. Não é necessário compilar o servidor para cadastrar nomes, capas, sinopses ou modos. Limites atuais: 4.096 IDs, 32 perfis por item, registro até 16 MiB; crescimento além disso exige ampliar capacidade com avaliação própria.

O catálogo publicado passou por duas varreduras privadas consecutivas sem mudanças. A execução agendada efetiva de 14:06:41 (America/Maceio) terminou com status0; a conferência de 17:07:19UTC confirmou índice, identidades e perfis byte idênticos, timer ativo e políticas automáticas efetivas. O recibo é `importer-effective.json`. As identidades são calculadas fora das requisições de download. Identidade de conteúdo é necessária para impedir conectar jogos/versões diferentes na mesma partida; não é verificação adicional para baixar.

## Provas realizadas e capacidade

**1.250 verificações C#**, **172 TLS loopback**, **91 v2 + 589 observabilidade**, testes Python de importação/conjuntos e **10 verificações Java/Python CUE** passaram. A implantação testou versão anterior e candidata em serviços isolados com o sandbox efetivo, depois catálogo, capas, downloads, v1/v2 e v3 autenticados em HTTPS/WSS público. Recibos exatos em `online-plataformas-20261009/production-applied.json` e `server-build-tests.json`.

O v3 admite até 100 salas e cinco pessoas conforme o modo. O orçamento compartilhado de replay v2/v3 foi configurado em **128 MiB**, mantendo janela de 256 KiB por direção. Prova sintética: **320 participantes em 80 salas de quatro**, 240 ligações, 7.864.320 bytes idênticos em ambas as direções e memória liberada ao sair. O orçamento também limita admissões: 100 salas de cinco simultâneas excederiam 128 MiB. Isso não mede internet, Android, latência ou estabilidade prolongada.

Índice/IDs/metadados/capas/ROMs/downloads/licenças/schema/segredos/outros serviços foram preservados; só o Station e seu importador foram atualizados. Proxy, Cloudflare e firewall permanecem iguais. Zero salas/conexões/replay no fechamento dos testes; retorno da release anterior preparado, sem restaurar banco. Não repetir operadores históricos nem o operador desta primeira implantação.

## Entrega compilada para produção do APK

`versions/station-all-platforms-online-20261009/` contém **212 fontes Java**, DEX, quatro cores ARM64/API26/16 KiB, overlays, runtime de cinco, fontes/licenças e receitas completas. Client DEX (login/catálogo/download) segue byte idêntico: `e7207a89517b168cc476e30a823bed3a1b6ad57339e1042f08a4660184543d63`. Rooms DEX: `9f13b23f1442dfa4f1ac5393195705a01e78a30e8b32c9f27d1f346f56f7e80f`. Runtime: `81b3daa38fb9c051e1c83646b87df7120f84f31ed8b48fe6b0b362b617b847dc`.

Perfis/controles Java: 200 verificações; BIOS isoladas: 21; CUE: 10. NeoCD remove ponteiros dependentes de ASLR do estado; estado de 8.231.939 bytes e continuação passaram entre processos. N64 fixa `parallel-n64-rtc-savestate=enabled`; estado de 16.790.604 bytes e quatro portas passaram entre processos, com renderizador/CPU fixados. Desempenho no celular ainda precisa ser medido. Fontes e licença própria do FBNeo estão incluídas; sua licença contém restrição sobre lucro monetário.

Montar o APK completo no PCAPK com `recipes/package_candidate.py`, base privada R81 SHA `85fac8f51daa3a370eabb14d98832f75c2f30ff81422c549538ee715627032c6` e certificado original `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`. Saída em E:, backup em G:, instalar com `adb install -r` e preservar dados. A base/keystore estão no PCAPK, indisponível nesta sessão; nenhum APK completo foi assinado ou telefone instalado aqui. Esta receita e os binários novos são necessários para os motores adicionais; apenas recadastrar no servidor não modifica o APK instalado.

Pendências do **cliente**, reunidas nesta mesma entrega: Flycast quatro, ligação Station↔Dolphin/ENet, transporte Wii U, modo/motor Switch, montagem/assinatura/instalação e testes de controles/sincronização/retomada nos celulares. O servidor dessas cinco plataformas está concluído e o cadastro dos motores reais será feito por dados, sem recompilação/reinício. Não inventar hashes nem declarar gameplay Android antes da próxima implementação/teste do app. As pendências antigas de BIOS proprietária NeoCD, conjunto `aof2.zip` incompleto e 65 referências XML ausentes continuam registradas; nada foi apagado ou fabricado.

## Arquivos para comparação completa

`server-integration-contract.json`: contrato completo do servidor para a próxima integração do app. `server-prepared-modes.json`: todos os 273 jogos restantes/274 modos cruzados com conteúdo/capas, sem hashes de motor inventados. `server-ready-deployment.json`: publicação efetiva e provas finais, incluindo preservação da sala humana que abriu durante a primeira conferência.

`catalogo-cruzado-completo.json`: todos os IDs/nomes/plataformas/capas/metadados/artefatos/identidades e estado online, sem caminhos privados. `content-identities.json`: 3.848 vínculos, schema2. `profiles-complete.json`: os 5.176 perfis efetivos. `profiles-new-profiles.json`: somente as 1.504 adições; não substituir o registro completo por ele. `modes-authorized.json`: dados dos modos novos. `platform-summary.json`: os 17 rótulos e contagens. `profiles-missing.json`: itens sem motor/modo atual. `production-applied.json`: publicação e provas reais. `engines-app.json`: hashes dos motores compilados. Demais recibos de Java/BIOS/cores/estado e backup acompanham o mesmo diretório.

## Fontes primárias

- [Super Smash Bros. Melee, Nintendo](https://www.nintendo.com/en-gb/Games/Nintendo-GameCube/Super-Smash-Bros-Melee-268951.html).
- [Super Mario Galaxy e Co-Star, Nintendo](https://www.nintendo.com/en-gb/Games/Wii/Super-Mario-Galaxy-283322.html); [manual Galaxy 2](https://m1.nintendo.net/docvc/RVL/EUR/SB4P/SB4P_E.pdf).
- [Pokémon Café ReMix: quantidade de jogadores, suporte oficial](https://app-pcm.pokemon-support.com/hc/en-us/articles/360053180271--How-many-players-can-play-the-game).

- [Mario Kart 64, manual Nintendo](https://www.nintendo.com/eu/media/downloads/games_8/emanuals/nintendo_8/Manual_Nintendo64_MarioKart64_EN.pdf).
- [F-Zero X, manual Nintendo](https://www.nintendo.com/eu/media/downloads/games_8/emanuals/nintendo_8/Manual_Nintendo64_FZeroX_EN.pdf).
- [Mario Tennis, manual Nintendo](https://www.nintendo.com/eu/media/downloads/games_8/emanuals/nintendo_8/Manual_Nintendo64_MarioTennis_EN.pdf).
- [Bomberman 64, Nintendo](https://www.nintendo.com/es-es/Juegos/Nintendo-64/Bomberman-64-1204884.html).
- [Dolphin Android NetPlay 2609](https://alwaysdata.dolphin-emu.org/blog/2026/09/24/dolphin-progress-report-release-2609/) e [sessão nativa embarcada](https://github.com/dolphin-emu/dolphin/blob/5102a0339c2177575378107b76541e47cc52122d/Source/Android/app/src/main/java/org/dolphinemu/dolphinemu/features/netplay/model/NetplaySession.kt).
- [Flycast GGPO embarcado](https://github.com/flyinghead/flycast/blob/e36e9df2dcc1487acdb1dc7725766f1f5ba029b5/core/network/ggpo.cpp).
- [Drivers FBNeo fixados](https://github.com/finalburnneo/FBNeo/tree/95153da1f113c56735bd9a818171f908df628421/src/burn/drv); fontes específicas preservadas em cada modo.
- [PCSX ReARMed](https://docs.libretro.com/library/pcsx_rearmed/) e [FBNeo Libretro](https://docs.libretro.com/library/fbneo/).
