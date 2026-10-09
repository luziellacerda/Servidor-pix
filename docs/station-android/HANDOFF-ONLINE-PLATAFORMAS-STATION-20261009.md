# Station: backup e integração de plataformas — 09/10/2026

## Estado desta entrega

A cópia anterior foi publicada **antes de alterar qualquer fonte** nos dois repositórios. A ampliação está em branches próprias. **Esta entrega é candidata compilada; não foi implantada na produção nem instalada nos telefones. O pedido de colocar todas as plataformas online ainda não está concluído.**

O Station ativo continua com a release `ad45a4f0`, catálogo revisão 24 e 3.672 perfis anteriores. Nenhum desses perfis foi revogado. Canais, certificados, licenças, saves, downloads, capas, importador, túnel, proxy, firewall e outros produtos foram preservados. `production-preserved.json` contém a leitura do processo e das sessões no fechamento.

## Cópia preservada

Em `luziellacerda/Servidor-pix` e `luziellacerda/TurboElden`:

- Branch: `backup/station-online-antes-todas-plataformas-20261009`.
- Tag anotada: `backup-station-online-20261009-antes-todas-plataformas`.
- Servidor: `2b04f591eb10ad76efc3b630ded4fff9ef2c2a27`.
- App: `4521218492ec65c33398aed07893f95e2d7f499d`.
- Push atômico e referências remotas conferidos. A tag preserva o estado existente, incluindo suas pendências de APK/teste físico; não declara homologação de cinco aparelhos.

Branches de trabalho: servidor `feat/station-online-all-platforms-20261009`; app `feat/station-online-all-platforms-client-20261009`.

## Limites solicitados e implementação

| Plataforma | Limite | Estado verificado |
|---|---:|---|
| SNES, incluindo BR | 5 | Serviço anterior preservado; runtime de cinco já compilado no pacote anterior. APK completo/teste físico de cinco continuam pendentes. |
| Mega Drive, incluindo BR | 2 | Limite mantido no servidor e no parser Android candidato. |
| N64 | 4 | Novo motor ARM64, quatro portas exclusivas, controle analógico e transferência de estado entre processos passaram. Perfis/modos e teste em quatro celulares pendentes. |
| Neo Geo, FBNeo, CPS1/2/3 | 2 | Motor FBNeo compilado, ZIP original suportado, controles e BIOS integrados na candidata; sem novo gameplay físico comprovado. |
| Neo Geo CD | 2 | Motor CHD compilado e estado corrigido; transferência entre dois processos com Metal Slug passou. Compatibilidade do HLE com toda a coleção não foi comprovada. |
| PlayStation 1 | 2 | PCSX ReARMed compilado com threads assíncronas desativadas. Digital e DualShock têm perfis próprios. 81 PBP/CHD suportados; três CUE dependem de identidade do conjunto de faixas. |
| Dreamcast | 4 | Limite implementado. O GGPO do Flycast embarcado ainda tem dois participantes; expansão nativa para quatro e integração das salas pendentes. |
| GameCube e Wii | 4 | Limite implementado. O Dolphin embarcado já tem NetPlay Android; falta conectá-lo automaticamente às salas Station e ao transporte ENet/UDP. |
| Wii U | 4 | Limite implementado. Cemu embarcado não tem integração Station de multiplayer local em rede; precisa de motor/transporte próprios. |
| Switch | 2 | Limite implementado. O único jogo atual é Pokémon Café Mix; modo local para duas pessoas não está confirmado. |

Os limites são tetos da plataforma. Somente um perfil aprovado com identidade exata e modo correto concede vagas. Metadado `players`, porta local ou capacidade de sala não prova suporte online. Ver detalhes continua somente consultando a sala, sem transmitir vídeo ou conceder controle.

## Implementado no servidor candidato

`StationMultiplayerPlatformPolicy` centraliza os limites e aliases. `StationMultiplayer` rejeita perfil acima do teto e publica `platformPolicy` dentro da resposta assinada. A falta de motor/perfil continua visível como `onlineAvailable:false`. Os contratos, autenticação, tickets, recuperação, limites de memória e admissão v1/v2 anteriores foram preservados.

Compilação passou: DLL candidata `8981510264c82bb6a5c9a536634ca0905807635dca675b0794997acf129c5620`; **504 verificações C#** passaram. Não é a DLL ativa.

## Catálogo e cadastro por dados

`online-plataformas-20261009/` contém:

- `catalogo-cruzado-completo.json`: 3.848 IDs, nomes, plataforma, coverId, descritores e metadados; sem caminhos internos. É projeção candidata revisão 25, **não resposta da produção**.
- `content-identities.json`: 3.575 vínculos exatos. Preservados os 2.071 anteriores; acrescentados 1.504 em oito plataformas. Contêineres, payload de lançamento, tamanho, membros e substituição de arquivos foram verificados fora das rotas de download.
- `platform-summary.json`: os 17 rótulos efetivos, incluindo BR, com limites, capas e identidades.
- `profiles-new-profiles.json`: 1.501 rascunhos com hashes de jogo/motor/runtime/controles; **zero novas aprovações**. Três CUE não viraram perfil.
- `profiles-missing.json`: três CUE pendentes; `profiles-receipt.json` comprova preservação dos 3.672 anteriores.

`qualificar-conteudos-online-plataformas.py` reutiliza o binder R81 já testado. É operação offline, sem adicionar checagem à espera do download. `preparar-perfis-online-plataformas.py` recebe catálogo, manifesto dos motores, registro anterior e, opcionalmente, modos revisados. Novos jogos não exigem programar nomes: entram como dados. Aprovar um modo exige os hashes exatos atuais; rótulos do XML nunca viram aprovação automática. O arquivo de modos define quantidade, controles, avisos e fontes; o mesmo cliente interpreta qualquer jogo compatível.

Para persistir identidades, a implantação deve atualizar o registro configurado do importador junto do índice, sob `scan.lock`, comparando o SHA original `6b8acfa4ca419ec705f48f53e2063633ff8f0a30a36cb8f4d651a108cbb31137`. Aplicar só a cópia do índice perderia as identidades numa varredura futura. Os operadores históricos não são a receita desta candidata. A implantação combinada de índice/registro/perfis/DLL ainda não foi executada.

## App candidato completo em fonte

Pacote `versions/station-all-platforms-online-20261009/`: **211 Java**, dois DEX, quatro cores ARM64/API26/16 KiB, runtime anterior intacto, overlays de N64/PSX/arcade, licenças, fontes correspondentes e receitas.

- Autenticação/catálogo/downloads: client DEX `e7207a89517b168cc476e30a823bed3a1b6ad57339e1042f08a4660184543d63`, idêntico ao anterior.
- Rooms DEX: `bec655c6714ec66e2b247be33ea7aa2dae1ca139c3eb6e4419d576341943ce37`.
- Runtime: `81b3daa38fb9c051e1c83646b87df7120f84f31ed8b48fe6b0b362b617b847dc`, idêntico ao pacote de cinco.
- **200 verificações Java de perfis/controles** e **21 verificações de BIOS** passaram.
- BIOS são copiadas exclusivamente dos assets já existentes no APK para pastas online separadas. Saves/configurações offline não entram na partida. NeoCD aceita o HLE incluído no novo core; isso não confirma todos os jogos.
- Só portas realmente ocupadas ficam conectadas no N64. O overlay inicia no analógico; avanço rápido fica desativado nos novos controles online.
- Neo Geo ZIP usa o novo FBNeo; o antigo Geolith exige .neo e não é selecionado por engano.

NeoCD: o estado original guardava ponteiros da CPU que mudavam entre processos. `native/patches/neocd-aslr-state.patch` remove esses endereços da representação salva; a restauração reconstrói as tabelas/callbacks locais. A reprodução do patch e as comparações antes/depois estão nos recibos. O estado de 8.231.939 bytes, vídeo e duas portas passaram após a correção.

N64: a opção padrão de relógio deixava a restauração consumir incorretamente parte do estado. A candidata fixa `parallel-n64-rtc-savestate = "enabled"`. Comparações com e sem a opção estão nos recibos; após a correção, estado de 16.790.604 bytes, vídeo e quatro portas passaram entre processos. O renderizador/código de CPU foram fixados para essa prova; desempenho em celulares ainda não foi medido.

Fontes/licenças dos quatro cores estão no pacote. O FBNeo tem licença própria com restrições sobre lucro monetário; o texto integral está em `native/licenses/fbneo.txt`. Não interpretar sua presença como autorização comercial para o projeto. Nenhuma ROM, BIOS proprietária, APK completo, senha ou chave foi enviada ao Git.

## Ações para concluir o pedido

1. Concluir as integrações nativas de Flycast quatro participantes, Dolphin Station/ENet e Wii U; revisar o jogo Switch. Os valores da tabela não substituem esses motores.
2. Montar o APK completo no PCAPK com `recipes/package_candidate.py`, base privada R81 SHA `85fac8f51daa3a370eabb14d98832f75c2f30ff81422c549538ee715627032c6`, certificado original `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`. Saídas em E:, backup em G:. A receita acrescenta cores com nomes próprios e preserva byte a byte todas as outras entradas, inclusive emuladores, mídias, licença e BIOS. APK completo não foi montado aqui porque a base e a assinatura privadas estão no PCAPK.
3. Revisar os modos e vincular suas aprovações aos hashes deste pacote. Não substituir o registro ativo pelo arquivo de novos rascunhos; combinar preservando os 3.672 anteriores.
4. Selar e testar a implantação combinada em sombra, com retorno pronto; só trocar o Station quando v1/v2/v3 estiverem sem salas/conexões/recuperação. Não reabrir aprovação genérica: a implementação já foi autorizada, mas Linux ainda exige autenticação administrativa para escrever nos arquivos protegidos.
5. Instalar com `adb install -r`, sem limpar dados, e conferir sincronização, controles, pausa, retomada e troca de anfitrião nos aparelhos. Repetir com 2/3/4 conforme o modo e cinco no SNES. Os testes de processo não homologam gameplay Android, WAN, latência ou centenas de usuários.

## Fontes primárias consultadas

- Dolphin Android NetPlay 2609: https://alwaysdata.dolphin-emu.org/blog/2026/09/24/dolphin-progress-report-release-2609/
- Dolphin embarcado, sessão nativa: https://github.com/dolphin-emu/dolphin/blob/5102a0339c2177575378107b76541e47cc52122d/Source/Android/app/src/main/java/org/dolphinemu/dolphinemu/features/netplay/model/NetplaySession.kt
- Flycast embarcado, `MAX_PLAYERS=2`: https://github.com/flyinghead/flycast/blob/e36e9df2dcc1487acdb1dc7725766f1f5ba029b5/core/network/ggpo.cpp
- Libretro PCSX ReARMed: https://docs.libretro.com/library/pcsx_rearmed/
- Libretro FBNeo: https://docs.libretro.com/library/fbneo/
- Mupen64Plus-Next e Flycast Libretro não são equivalentes ao transporte nativo: https://docs.libretro.com/library/mupen64plus/ e https://docs.libretro.com/library/flycast/
- NeoCD e ParaLLEl: revisões e arquivos exatos em `evidence/core-builds.json`; provas em `evidence/`.
