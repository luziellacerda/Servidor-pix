# Station: catálogo, capas, downloads e servidor online — 10/10/2026

## Estado atual confirmado na produção

**Servidor concluído para as 17 plataformas solicitadas**, incluindo PS2 e Sega Saturn. A produção do APK pode implementar os motores usando esta mesma entrega. Servidor pronto significa transporte, autenticação, perfis e cadastro; os motores reais e o gameplay nos aparelhos continuam com as dependências indicadas abaixo.

- API ativa: PID **2107490**, NRestarts **0**; release `/opt/turborama-station-ps2-saturn-20261010-1717ac5ecec9-c2831745`.
- DLL SHA-256 `1717ac5ecec9131cd53353be608027d533dada773b36f1e952f64fe28027b68d`; fonte C# `33cdb9821be9e73b9d3af51869b169f29bdb9e2f`; fonte do importador `37a767fea4cdf8056da8657a7fa23b98706c56b2`.
- Catálogo **revisão 31 / 3.895 IDs / 3.640 visíveis / 255 aliases**, SHA `d44705f559cc884ccdc63087d662ff8c10425c2677ed0cc1eefc601b9f4522e1`. São 19 rótulos / 17 plataformas normalizadas.
- **3.895 identidades**, **5.176 perfis / 5.169 aprovados**, preservando os perfis e os 3.876 vínculos de conteúdo anteriores. **320 jogos restantes / 321 modos preparados**.
- Publicação em `2026-10-10T19:50:05.223558+00:00`: somente Station reiniciado, com zero salas e conexões. HTTPS autenticado confirmou catálogo, as 17 políticas assinadas e amostras de mídia; timer/importador ativo, varredura final sem mudanças. Licenças sintéticas removidas; configurações anteriores, clientes, licenças reais, esquema e outros serviços preservados. Túnel/proxy/firewall inalterados.

[Recibo atual, jogos Saturn, capas, descritores, cabeçalhos e provas](online-plataformas-20261009/ps2-saturn-servidor-20261010.json). Este é o mesmo handoff; recibos antigos registram seus números e horários históricos.

## Plataformas e limites do servidor

| Plataforma | Máximo por sala | Dependência do app |
|---|---:|---|
| SNES / BR | 5 | Modos cadastrados, multitap para cinco e runtime correspondente. |
| Mega Drive / BR | 2 | Motores/controles anteriores preservados. |
| N64 | 4 | Perfis de duas/quatro portas e modo exato; não transformar campanha solo em multiplayer. |
| Dreamcast | 4 | Adaptador nativo Station; o GGPO de duas entradas não comprova quatro. |
| GameCube / Wii / Wii U | 4 | Adaptador nativo; GBA Link, controles compartilhados e Co-Star usam layouts próprios. |
| PS2 / Sega Saturn | 2 | Motores reais, sincronização, duas entradas e BIOS quando exigida. |
| Switch | 2 | Motor/adaptador real e modo local da edição. |
| Neo Geo / Neo Geo CD / PSX / FBNeo / CPS1 / CPS2 / CPS3 | 2 | Driver, BIOS e perfil exatos; exceções conhecidas preservadas. |

O limite da plataforma não substitui `allowedPlayerCounts` do perfil. Não abrir sala com mais entradas do que o modo oferece. `Ver detalhes` não transmite gameplay nem cria espectador. Sete vínculos continuam sem aprovação: seis conjuntos arcade sem associação exata e Worms Armageddon por turnos. O catálogo ainda registra aof2 incompleto, 65 referências XML sem ROM, 148 sinopses ausentes e o requisito de BIOS NeoCD; não foram fabricados arquivos/compatibilidade.

## PS2 resolvido no servidor

Os 25 ISO/CSO estão na raiz `ps2`, com revista, artes de cadastro, XML, seed, downloads raw e **política online de duas pessoas**. `ps2br`/PlayStation 2 BR normalizam para `ps2`. Layout: `ps2-two-controllers-v1`. Seus 25 modos foram preparados: 13 entradas individuais e 12 de duas vagas por política de uso; estas não são homologação de modo original ou Android. O motor real precisa respeitar cada edição/modo. Nenhum jogo foi associado ao motor PSX.

Os cores [Play!](https://docs.libretro.com/library/play/) e [LRPS2](https://docs.libretro.com/library/lrps2/) documentam ausência de netplay. Portanto o APK precisa fornecer um motor/adaptador capaz de estado sincronizado e controles remotos; somente cadastrar o nome PS2 não cria essa capacidade. A API responde `serverReady:true` e `online-engine-pending` até existir o vínculo real.

## Sega Saturn organizado e publicado

`snes/saturn` foi recortada para **`saturn` na raiz do HD**, ao lado de SNES/PS2/Mega. Pasta completa: `/media/lz-servidor/a2700961-7d8b-435f-9408-9132877ff0fc/saturn`.

- **19 jogos**: 17 CHD servidos raw pelo mesmo inode, sem segunda cópia, e Bug!/Rayman originalmente em 7z, preparados como ZIP completo. Bug! inclui CUE + 73 faixas; Rayman, CUE + 51 faixas. Originais preservados. Ambos possuem identidade `cue-set-v1`; o hash do CUE sozinho não representa o disco.
- **19 capas de revista**, 16 reutilizadas do catálogo Sega Saturn e três criadas com a ferramenta imagegen: Bomberman, Bug! e Rayman. Todas JPEG RGB 480×720; **34 artes de catálogo / 37 chaves** no seed. Prompts e caminhos finais no recibo. As imagens geradas originais ficam no backup privado; JPEG final em `saturn/media/revista`.
- Os 17 cabeçalhos CHD confirmaram Sega Saturn e os títulos correspondentes. `Bomberman.chd` identifica SATURN BOMBERMAN/MK-81070; Castlevania identifica DRACULA-X. Essa leitura de 4 KiB não é teste completo de disco ou emulação.
- Policy online pronta, teto dois, aliases Sega Saturn/segasaturn, layout `saturn-two-controllers-v1`. Há 19 modos preparados, respeitando as entradas individuais cadastradas; todos aguardam o motor real do APK.
- O core [Beetle Saturn](https://docs.libretro.com/library/beetle_saturn/) documenta netplay e formatos CUE/CHD, mas o APK ainda precisa integrar o adaptador Station, controles e BIOS legítima. A pasta `saturn/bios` está preparada; nenhuma BIOS proprietária foi baixada ou fornecida nesta entrega.

## SNES: mesmo cadastro automático

SNES mantém jogos, grupos, IDs e perfis online anteriores, inclusive até cinco nos modos cadastrados. `snes/station-catalog-seed.json` agora oferece **1.366 chaves não ambíguas**, reutilizando as **1.619 capas já comprimidas de revista**. Doze chaves conflitantes foram excluídas da escolha automática. XML/overrides têm prioridade; o seed preenche dados ausentes. Dois registros/aliases de Battletoads receberam campos de metadata que estavam vazios; descrição, ROM, capa e vínculo de partida foram preservados.

Para novo jogo, colocar a ROM na pasta da plataforma. O timer exige duas observações estáveis e idade mínima de 20 segundos, então publica nome, ID, capa, descritor e identidade. Capas conhecidas usam a associação por título; arte própria fica em `media/revista` com o nome exato da ROM. Dados complementares vêm de XML/seed/overrides. Título desconhecido usa a capa padrão existente até receber arte própria; uma sinopse não é inventada. Não é necessário programar cada jogo.

PS2 aceita ISO/CSO; Saturn, CHD/CUE/ISO e arquivos ZIP/7z/RAR com um CUE completo ou `launchPath` explícito. ISO é formato de publicação; o motor que vier precisa declarar o que realmente abre. Não inventar CUE/faixas. CHD/ISO raw de Saturn usa armazenamento protegido no mesmo volume, root/0444 e um inode. Substituir ROM significa copiar um arquivo novo e substituir o nome, preservando os artefatos de downloads anteriores. As outras plataformas mantêm suas configurações.

## Como o aplicativo deve ler o servidor

1. Usar `https://app.lzgames.com.br`, licença original, bearer e provas vinculadas ao aparelho. Ler e validar `GET /v1/station/catalog?metadata=1`. Usar a revisão recebida, `itemId`, `platform`, `name`, `metadata`, `coverId`, `folderPath`, `contentSha256` e eventual `contentIdentityScheme`; caminhos do HD não são URLs.
2. Buscar `/v1/station/covers/{coverId}` pela mesma linha do catálogo, quatro workers e cache `coverId + revisão do item`. As capas servidas são JPEG de 480×720. Não tentar casar capas pelo índice de outra lista.
3. Autorizar `/v1/station/downloads/authorize` com o itemId e validar seu descritor assinado `artifact`. Consumir `/v1/station/artifacts/{grantId}`. Raw preserva arquivo/extension/launchPath; ZIP extrai o pacote completo para um diretório exclusivo. Seguir tamanhos `long`, formato, fileCount e caminho de lançamento. Nenhuma espera, limite de velocidade ou verificação extra foi adicionada ao downloader.
4. Enviar `enter` autenticado em `/v1/station/online/command` antes de v3. Manter somente um poll de `online/events` por aparelho e respeitar `Retry-After`. Declarar `clientMaximumPlayers` conforme o runtime real.
5. Em `/v1/station/online/multiplayer/command`, usar `capabilities`/`snapshot`. `serverPlatforms` lista as 17 políticas. `platformPolicy.serverReady:true` é preparação do servidor; `onlineAvailable` requer perfil aprovado de conteúdo/motor/runtime/controles. `online-engine-pending` indica o motor real ainda ausente. Tratar também `single-player`, `mode-pending`, `content-identity-pending` e `select-game`.
6. Selecionar perfil exato e quantidade em `allowedPlayerCounts`; criar/entrar/pronto/iniciar. Convites e tickets são automáticos. Conectar WSS `/v1/station/online/multiplayer/relay`, protocolo `station-stream.v3`, um link duplex anfitrião↔cada convidado.
7. Respeitar TSR3, HELLO, PAUSED, READY, ACK e época global. ACK confirma escrita no motor. Preservar sessão nativa na troca do WSS. Identidade CUE/CHD é calculada na preparação/importação, sem varredura do corpo em cada download do servidor; o app compara a identidade para conectar versões iguais na partida.

## Cadastro do motor real sem nova DLL

O manifesto recebido do APK deve declarar engineId imutável, plataforma, formatos reais, `library`, `runtimeLibrary`, seus hashes SHA-256, `launchReady:true`, `recoveryProtocol:station-stream.v3`, `maximumPlayers` e `controllerProfiles`. Para PS2/Saturn, declarar os layouts acima explicitamente. Cada layout contém `configuration` com `schemaVersion`, `controllerProfile`, `devices`, `coreOptions`. O motor/decoder do app deve implementar essa configuração; nomes de controles por si só não representam sua implementação.

`profileSha256` é SHA-256 do JSON compacto UTF-8, sem BOM/quebra final, Unicode sem escapes e campos na ordem `schemaVersion`, `controllerProfile`, `devices`, `coreOptions`. Exemplos do contrato não são recibos nativos. Não copiar hashes ilustrativos para a produção.

Operador instalado: `/opt/turborama-station-ps2-saturn-20261010-1717ac5ecec9-c2831745/library-tools/cadastrar-motor-online-station.py install`, com `--config` da configuração privada efetiva, `--incoming` do manifesto real, `--artifacts` dos binários e `--output` de uma pasta privada nova. Exige administração local, confere ambos os binários e preserva perfis anteriores. `prepare` permite revisão offline; `apply` publica a candidata ainda correspondente ao catálogo. **Recarga em até dez segundos, sem recompilar ou reiniciar o servidor.** Credenciais do app não cadastram motores pela API.

## Testes, capacidade e entrega ao APK

Nesta atualização passaram **1.292 verificações C#**, **172 TLS loopback**, **16 testes Python** de cadastro/importação/permissões e **855 verificações v3 assinadas na sombra**: início, dados nos dois sentidos, retomada e perfis SNES de duas/quatro/cinco pessoas. As 17 políticas e o catálogo/mídia também foram conferidos pelo HTTPS público após ativação. Testes sintéticos não homologam latência, controles ou gameplay Android.

Capacidade configurada: v3 até 100 salas, cinco pessoas conforme modo, janela 256 KiB por direção e replay compartilhado 128 MiB; v1/v2 preservados. Teste sintético de 320 participantes/80 salas de quatro/7.864.320 bytes passou. O orçamento limita admissões; não significa 100 salas de cinco simultâneas ou medição da internet.

O pacote `versions/station-all-platforms-online-20261009` foi recompilado: **212 Java/D8**, alias PS2 BR corrigido. Client DEX permanece `e7207a89517b168cc476e30a823bed3a1b6ad57339e1042f08a4660184543d63`; Rooms DEX agora `04eb0fe853c6e485c69e69a18ee9b1551b3fc7422d6189fff9f431cabb655462`. Runtime `81b3daa38fb9c051e1c83646b87df7120f84f31ed8b48fe6b0b362b617b847dc` e motores reais anteriores preservados. Nenhum APK completo foi assinado/instalado nesta sessão; base/keystore continuam no PC do APK. Próxima etapa reúne Dreamcast, GameCube, Wii, Wii U, Switch, PS2 e Saturn, assinatura/instalação e testes físicos.

## Arquivos únicos para comparar e retomar

- `catalogo-cruzado-completo.json`: todos os 3.895 IDs, nomes, plataformas, capas, metadata, artefatos, identidades, perfis e modos preparados; sem caminhos privados de mídia.
- `platform-summary.json`, `profiles-missing.json`: contagens por plataforma e dependências reais.
- `content-identities.json`: 3.895 vínculos/schema2; `profiles-complete.json`: todos os 5.176 perfis. Não substituir por `profiles-new-profiles.json`, que contém só adições históricas.
- `server-integration-contract.json`, `server-prepared-modes.json`: contrato v3 completo, 320 jogos/321 modos, layouts, hashes e receita de cadastro.
- `engines-app.json`: motores reais atuais e políticas do cliente; nenhum engine PS2/Saturn fictício.
- `library-tools/`: scanner, todas as dependências, operador de cadastro e testes correspondentes.
- `ps2-saturn-servidor-20261010.json`: recibo atual desta atualização. `ps2-switch-publicados-20261010.json`, `capas-padronizadas-20261010.json`, `checkup-completo-20261010.json`, `server-ready-deployment.json`, `production-applied.json` e `profile-reload-fix.json` preservam as provas históricas. ZIP de 09/10 continua histórico e não representa o catálogo31.

Cópia estável anterior publicada antes das mudanças nos dois Gits: branch `backup/station-antes-online-ps2-saturn-20261010`, tag `backup-station-antes-online-ps2-saturn-20261010`, servidor `26c5302`, app `52ba50d`. Backup privado atual: `/mnt/DADOS/station-ps2-saturn-20261010/backup`. Não repetir `apply.py` nem operadores datados. Consultar o estado dinâmico das salas antes de agir sobre serviços. Os dois arquivos CRLF das entregas R71/R78 anteriores ficaram fora desta alteração.
