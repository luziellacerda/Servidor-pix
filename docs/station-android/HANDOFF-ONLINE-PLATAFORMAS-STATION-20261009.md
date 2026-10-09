# Station: publicação completa do servidor e integração do app — 09/10/2026

## Resultado efetivo

**Atualização aplicada e conferida pelo domínio público.** DLL `e74fde53cbe65273d26b3576cb39b8d096dff476dff99409681cd2469b678007`, fonte `31fc7790f410c524c54a63e2cae15272b960e5ba`, PID **1805466**, NRestarts **0**, recarga `2026-10-09T17:01:55.811112+00:00` e conclusão `2026-10-09T17:04:31.845345+00:00`. Catálogo **revisão 27 / 3.848 IDs / 3.593 visíveis / 255 aliases**, identidade de conteúdo de todos os 3.848 itens e **5.176 perfis**, preservando exatamente os **3.672 anteriores**. Foram acrescentados 1.504 perfis de oito plataformas. Aprovação de uso não declara gameplay Android homologado.

O servidor está publicado para SNES, Mega Drive, N64, Neo Geo, Neo Geo CD, PlayStation, FBNeo, CPS1, CPS2 e CPS3, nos modos e formatos cadastrados. Dreamcast, GameCube, Wii e Wii U já têm seus tetos e dados reconhecidos; a conexão dos motores dessas plataformas ao Station ainda depende do app. Switch possui somente Pokémon Café Mix no catálogo, sem modo local de duas pessoas confirmado. **Não anunciar todas as plataformas jogáveis nos celulares.**

Backup anterior publicado antes das alterações, em ambos os repositórios: branch `backup/station-online-antes-todas-plataformas-20261009`, tag anotada `backup-station-online-20261009-antes-todas-plataformas`; servidor `2b04f591eb10ad76efc3b630ded4fff9ef2c2a27`, app `4521218492ec65c33398aed07893f95e2d7f499d`. Branches atuais: `feat/station-online-all-platforms-20261009` no servidor e `feat/station-online-all-platforms-client-20261009` no app. Código cliente compilado em `cc74549`; use a pasta atual desta entrega para seus recibos posteriores.

## Plataformas e vagas reais

| Plataforma | Teto do servidor | Cadastro e requisito do app |
|---|---:|---|
| SNES / BR | 5 | Perfis anteriores preservados; cinco somente nos modos cadastrados e com runtime novo. |
| Mega Drive / BR | 2 | Dois; controles/portas do pacote anterior preservados. |
| N64 | 4 | Motor e quatro portas compilados; quatro modos documentados cadastrados. Outros modos ficam em uma ou duas vagas. |
| Neo Geo / FBNeo / CPS1/2/3 | 2 | FBNeo ARM64 compilado; quantidade limitada pelo driver exato e pelo cadastro individual. Seis conjuntos sem driver exato permanecem sem sala online. |
| Neo Geo CD | 2 | NeoCD compilado e estado entre processos corrigido. HLE não comprova compatibilidade de toda a coleção. |
| PlayStation 1 | 2 | PCSX compilado; PBP, CHD e três CUE com identidade de todas as faixas. Somente modos de controles distintos cadastrados; Worms Armageddon aguarda passagem de controle por turnos. |
| Dreamcast | 4 | Falta integração real do Flycast para quatro participantes; o GGPO embarcado contém duas entradas. |
| GameCube / Wii | 4 | Dolphin tem NetPlay Android; falta ligação automática da sala Station ao ciclo JNI e transporte ENet/UDP. |
| Wii U | 4 | Conjunto completo identificado; Cemu atual não tem transporte Station de multiplayer local. |
| Switch | 2 | Falta jogo/modo multiplayer local comprovado e integração do motor. |

Os tetos não concedem controles em campanhas individuais. Salas têm `allowedPlayerCounts`, `modeTitle`, `instructions`, perfil de portas e hashes exatos. `Ver detalhes` consulta a sala, sem assistir ao vídeo nem ganhar controle. Os modos N64 documentados são Bomberman 64 Batalha (2–4), Mario Kart 64 VS/Battle (2–4; GP no máximo 2), F-Zero X VS Battle (2–4) e Mario Tennis Exhibition em duplas (2 ou 4 no cadastro). SNES mantém os modos de Bomberman 1/2/3 anteriores; Bomberman 4/5 ainda não são ROMs presentes neste catálogo.

## Como o app deve consumir a produção

1. Usar o domínio `https://app.lzgames.com.br`, login/licença original, bearer e provas vinculadas ao aparelho. Contratos v1/v2, dez engines anteriores e flags de admissão permanecem preservados.
2. Ler `GET /v1/station/catalog?metadata=1` autenticado e validar a resposta assinada. A revisão atual é 27. Usar `itemId`, `platform`, `revision`, `coverId`, `artifact`, `metadata` e `contentSha256` recebidos; não construir nomes/IDs a partir de capas ou pastas.
3. Pedir capas em `GET /v1/station/covers/{coverId}`, usando o `coverId` daquele item. Manter os quatro workers já implementados. Sinopses vêm de `metadata.description`; lacunas podem ser preenchidas por dados do servidor.
4. Autorizar em `POST /v1/station/downloads/authorize` e consumir a URL/grant retornados em `GET /v1/station/artifacts/{grantId}`. Seguir o descritor de arquivo/pacote e seu `launchPath` completo. CUE acompanha suas faixas; Wii U acompanha `code/content/meta`. Nenhum throttling, espera ou verificação nova foi acrescentado ao downloader.
5. Para salas v3, usar `POST /v1/station/online/multiplayer/command` com as provas existentes. Consultar o jogo e interpretar `platformPolicy.onlineAvailable`, `availability`, `maximumPlayers` e os perfis assinados. Estados: `available`, `single-player`, `mode-pending`, `content-identity-pending`, `online-engine-pending`, `select-game`.
6. Escolher o perfil exato de conteúdo/motor/runtime/controles e um valor de `allowedPlayerCounts`. Executar criar/entrar/pronto/iniciar e usar os tickets retornados em WSS `/v1/station/online/multiplayer/relay`. Os convites e tickets são tratados pelo app; não pedir que o jogador digite um segredo extenso.
7. `contentIdentityScheme` é opcional. Sem ele, permanece o hash do payload de lançamento já usado. Com `cue-set-v1`, `StationContentIdentity.java` calcula a identidade do CUE e das faixas **somente ao preparar a partida online**. `wiiu-set-v1` identifica todo o conjunto `code/content/meta`, para o futuro motor Wii U.

## Importação automática e persistência

O timer do importador continua ativo. Após duas observações de arquivo estável, um jogo novo entra com ID estável, capa relacionada, descritor e identidade offline. O importador mantém o registro persistente junto do índice e publica os perfis por dados antes de publicar o catálogo. A API recarrega o registro a cada dez segundos, sem reiniciar nem descartar salas em andamento; arquivo parcial/inválido mantém o último registro válido.

Política expressamente autorizada pelo mantenedor: jogos compatíveis recém-importados recebem até duas vagas, respeitando indicação individual e motor único disponível. Arcade novo com driver ainda não associado fica reconhecido no catálogo e aguarda associação por dados. Acima de duas pessoas exige modo/portas específicos. Não é necessário compilar o servidor para cadastrar nomes, capas, sinopses ou modos. Limites atuais: 4.096 IDs, 32 perfis por item, registro até 16 MiB; crescimento além disso exige ampliar capacidade com avaliação própria.

O catálogo publicado passou por duas varreduras privadas consecutivas sem mudanças. A execução agendada efetiva de 14:06:41 (America/Maceio) terminou com status0; a conferência de 17:07:19UTC confirmou índice, identidades e perfis byte idênticos, timer ativo e políticas automáticas efetivas. O recibo é `importer-effective.json`. As identidades são calculadas fora das requisições de download. Identidade de conteúdo é necessária para impedir conectar jogos/versões diferentes na mesma partida; não é verificação adicional para baixar.

## Provas realizadas e capacidade

**1.079 verificações C#**, **172 TLS loopback**, **91 v2 + 589 observabilidade**, testes Python de importação/conjuntos e **10 verificações Java/Python CUE** passaram. A implantação testou versão anterior e candidata em serviços isolados com o sandbox efetivo, depois catálogo, capas, downloads, v1/v2 e v3 autenticados em HTTPS/WSS público. Recibos exatos em `online-plataformas-20261009/production-applied.json` e `server-build-tests.json`.

O v3 admite até 100 salas e cinco pessoas conforme o modo. O orçamento compartilhado de replay v2/v3 foi configurado em **128 MiB**, mantendo janela de 256 KiB por direção. Prova sintética: **320 participantes em 80 salas de quatro**, 240 ligações, 7.864.320 bytes idênticos em ambas as direções e memória liberada ao sair. O orçamento também limita admissões: 100 salas de cinco simultâneas excederiam 128 MiB. Isso não mede internet, Android, latência ou estabilidade prolongada.

Índice/IDs/metadados/capas/ROMs/downloads/licenças/schema/segredos/outros serviços foram preservados; só o Station e seu importador foram atualizados. Proxy, Cloudflare e firewall permanecem iguais. Zero salas/conexões/replay no fechamento dos testes; retorno da release anterior preparado, sem restaurar banco. Não repetir operadores históricos nem o operador desta primeira implantação.

## Entrega compilada para produção do APK

`versions/station-all-platforms-online-20261009/` contém **212 fontes Java**, DEX, quatro cores ARM64/API26/16 KiB, overlays, runtime de cinco, fontes/licenças e receitas completas. Client DEX (login/catálogo/download) segue byte idêntico: `e7207a89517b168cc476e30a823bed3a1b6ad57339e1042f08a4660184543d63`. Rooms DEX: `9f13b23f1442dfa4f1ac5393195705a01e78a30e8b32c9f27d1f346f56f7e80f`. Runtime: `81b3daa38fb9c051e1c83646b87df7120f84f31ed8b48fe6b0b362b617b847dc`.

Perfis/controles Java: 200 verificações; BIOS isoladas: 21; CUE: 10. NeoCD remove ponteiros dependentes de ASLR do estado; estado de 8.231.939 bytes e continuação passaram entre processos. N64 fixa `parallel-n64-rtc-savestate=enabled`; estado de 16.790.604 bytes e quatro portas passaram entre processos, com renderizador/CPU fixados. Desempenho no celular ainda precisa ser medido. Fontes e licença própria do FBNeo estão incluídas; sua licença contém restrição sobre lucro monetário.

Montar o APK completo no PCAPK com `recipes/package_candidate.py`, base privada R81 SHA `85fac8f51daa3a370eabb14d98832f75c2f30ff81422c549538ee715627032c6` e certificado original `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`. Saída em E:, backup em G:, instalar com `adb install -r` e preservar dados. A base/keystore estão no PCAPK, indisponível nesta sessão; nenhum APK completo foi assinado ou telefone instalado aqui. Esta receita e os binários novos são necessários para os motores adicionais; apenas recadastrar no servidor não modifica o APK instalado.

Pendências do **cliente**, reunidas nesta mesma entrega: Flycast quatro, ligação Station↔Dolphin/ENet, transporte Wii U, modo/motor Switch, montagem/assinatura/instalação e testes de controles/sincronização/retomada nos celulares. O servidor já publica dados e transporte para os novos vínculos reais quando esses motores existirem; não inventar hashes de motores nem anunciar essas cinco plataformas prontas. As pendências antigas de BIOS proprietária NeoCD, conjunto `aof2.zip` incompleto e 65 referências XML ausentes continuam registradas; nada foi apagado ou fabricado.

## Arquivos para comparação completa

`catalogo-cruzado-completo.json`: todos os IDs/nomes/plataformas/capas/metadados/artefatos/identidades e estado online, sem caminhos privados. `content-identities.json`: 3.848 vínculos, schema2. `profiles-complete.json`: os 5.176 perfis efetivos. `profiles-new-profiles.json`: somente as 1.504 adições; não substituir o registro completo por ele. `modes-authorized.json`: dados dos modos novos. `platform-summary.json`: os 17 rótulos e contagens. `profiles-missing.json`: itens sem motor/modo atual. `production-applied.json`: publicação e provas reais. `engines-app.json`: hashes dos motores compilados. Demais recibos de Java/BIOS/cores/estado e backup acompanham o mesmo diretório.

## Fontes primárias

- [Mario Kart 64, manual Nintendo](https://www.nintendo.com/eu/media/downloads/games_8/emanuals/nintendo_8/Manual_Nintendo64_MarioKart64_EN.pdf).
- [F-Zero X, manual Nintendo](https://www.nintendo.com/eu/media/downloads/games_8/emanuals/nintendo_8/Manual_Nintendo64_FZeroX_EN.pdf).
- [Mario Tennis, manual Nintendo](https://www.nintendo.com/eu/media/downloads/games_8/emanuals/nintendo_8/Manual_Nintendo64_MarioTennis_EN.pdf).
- [Bomberman 64, Nintendo](https://www.nintendo.com/es-es/Juegos/Nintendo-64/Bomberman-64-1204884.html).
- [Dolphin Android NetPlay 2609](https://alwaysdata.dolphin-emu.org/blog/2026/09/24/dolphin-progress-report-release-2609/) e [sessão nativa embarcada](https://github.com/dolphin-emu/dolphin/blob/5102a0339c2177575378107b76541e47cc52122d/Source/Android/app/src/main/java/org/dolphinemu/dolphinemu/features/netplay/model/NetplaySession.kt).
- [Flycast GGPO embarcado](https://github.com/flyinghead/flycast/blob/e36e9df2dcc1487acdb1dc7725766f1f5ba029b5/core/network/ggpo.cpp).
- [Drivers FBNeo fixados](https://github.com/finalburnneo/FBNeo/tree/95153da1f113c56735bd9a818171f908df628421/src/burn/drv); fontes específicas preservadas em cada modo.
- [PCSX ReARMed](https://docs.libretro.com/library/pcsx_rearmed/) e [FBNeo Libretro](https://docs.libretro.com/library/fbneo/).
