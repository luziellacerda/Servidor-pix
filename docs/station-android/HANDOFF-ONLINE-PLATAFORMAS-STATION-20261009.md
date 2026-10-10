# Station: catálogo, capas, downloads e servidor online — 10/10/2026

## Estado atual confirmado na produção

**Servidor concluído para as 17 plataformas solicitadas**, incluindo PS2 e Sega Saturn. A produção do APK pode implementar os motores usando esta mesma entrega. Servidor pronto significa transporte, autenticação, perfis e cadastro; os motores reais e o gameplay nos aparelhos continuam com as dependências indicadas abaixo.

- API ativa: PID **2136303**, NRestarts **0**; release `/opt/turborama-station-endpoints-20261010-2c0d03447696`.
- DLL SHA-256 `2c0d034476969cbed58b519b744e841111373f89a4bcd6abb9cd14543abcde35`; fonte do servidor e importador `7eb0e009d40ce01db86aea9c489937e9bd2fd00a`; scanner SHA-256 `7f6e9ebb6e1ccc71c184e7d3e07cac856f1c0ceee4e281e272061406619e36bd`.
- Catálogo **revisão 32 / 3.940 IDs / 3.685 visíveis / 255 aliases**, SHA `36aeaf6c5b4167e6eb7332880c0558824b473b7a41215bcd79ed04439344ffa5`. São 21 rótulos / 19 plataformas normalizadas no catálogo; 17 políticas online permanecem vigentes.
- **3.940 identidades**, **5.176 perfis / 5.169 aprovados**, preservando os perfis e os 3.895 vínculos de conteúdo anteriores. **320 jogos restantes / 321 modos preparados**.
- Revisão de endpoints publicada em `2026-10-10T21:49:22.368044+00:00`: somente Station reiniciado após confirmar zero salas e conexões. HTTPS autenticado confirmou os 3.640 itens e as 17 políticas, 19 capas com quatro workers e 19 amostras de download. Timer/importador ativo; varredura real sem mudanças. Licenças sintéticas removidas; configurações anteriores, clientes, licenças reais, esquema e outros serviços preservados. Túnel/proxy/firewall inalterados.

[Recibo anterior da revisão e ativação](online-plataformas-20261009/endpoint-review-20261010.json). [Organização Saturn, capas, descritores e cabeçalhos](online-plataformas-20261009/ps2-saturn-servidor-20261010.json). Este é o mesmo handoff; os recibos anteriores preservam seus números e horários históricos.

## Sega 32X e PSP — catálogo, capas e downloads publicados

Aplicado em `2026-10-10T22:40:20.495314+00:00`, sem reiniciar a API: **36 jogos de Sega 32X e nove de PSP**. Pastas inteiras movidas de `snes/sega32x` e `snes/psp` para `sega32x` e `psp` na raiz do HD. Os 611 arquivos originais recebidos, XML, imagens, vídeos e demais dados permaneceram no conjunto.

**Capas:** 45 JPEG 480×720 em `media/revista`; nenhuma arte nova gerada. Sega 32X reutiliza suas 36 imagens recebidas. PSP reutiliza quatro artes correspondentes do catálogo Turborama e cinco imagens recebidas. As 21 artes PSP do catálogo e as demais artes XML foram preparadas em `media/catalogo`; os seeds registram 464 chaves PSP e 43 de 32X, excluindo associações ambíguas. Os 235 registros XML de PSP não são 235 ROMs: somente os nove arquivos presentes foram publicados.

**Downloads:** PSP oferece CSO bruto, com nome/extensão e descritor assinados. Armazenamento `readonly-hardlink` mantém o mesmo inode da ROM, root/0444, sem duplicar os 6,53 GB de jogos; a verificação inicial é offline, sem nova passagem de integridade em cada download do app. Sega 32X oferece ZIP com `launchPath` exato para `.32x`. Índice, identidades e configuração foram qualificados antes da publicação; todos os 3.895 itens anteriores e os registros online permaneceram iguais. Segunda varredura isolada e scanner real: `changed=false`, `added=0`, `updated=0`.

**Como o app lê:** abrir sessão/prova de aparelho pelo contrato existente e buscar `GET /v1/station/catalog?metadata=1`, verificando envelope e domínio. Usar `itemId`, plataforma explícita, revisão, `coverId`, metadados e `folderPath` da resposta. Buscar capas por `/v1/station/covers/{coverId}`, mantendo os quatro workers atuais e cache `coverId + revisão`. Autorizar cada download por `POST /v1/station/downloads/authorize`; usar o `grantId` uma vez em `/v1/station/artifacts/{grantId}` e executar o `launchPath` do descritor assinado. Nunca construir caminhos pela grafia do nome ou converter CSO para ISO. A recarga do índice publicado pode levar até dez segundos; atualizar a leitura do catálogo nesse intervalo.

O parser do **cliente já compilado** leu os 3.685 itens reais e validou as 45 associações de plataformas/descritores/sinopses; mapping `psp`/`sega32x` já existe, sem alteração de Java/DEX por estes dados. API pública assinada confirmou todos os campos, as 45 capas com quatro workers e 45 amostras de download de até 64 KiB, com grants de uso único. O sandbox efetivo leu 7759 arquivos e negou os quatro caminhos protegidos. São provas de catálogo/transporte; teste físico do APK não foi executado aqui.

Novos jogos dessas pastas entram pelo importador/timer existente, após estabilidade dos arquivos. Sinopses vêm de XML, seed ou override privado; capas correspondem por stem exato/seed sem associação ambígua. `metadata.players` é informativo: PSP pode indicar ad hoc e 32X pode indicar turnos. Esses números não aprovam vagas online. Esta inclusão entrega catálogo/capas/downloads; suas políticas/motores online exigem integração nativa própria. As 17 políticas anteriormente implementadas permanecem intactas.

[Recibo, cruzamento das artes, descritores e provas](online-plataformas-20261009/sega32x-psp-publicados-20261010.json). Cópia estável anterior já publicada nos dois Gits: branch/tag `backup/station-antes-sega32x-psp-20261010` / `backup-station-antes-sega32x-psp-20261010`, servidor `6dbc9e2`, app `0d51ce1`.

## Limpeza do HD DADOS — concluída

Removidos **117 caminhos de compilações antigas, cópias de qualificação e arquivos intermediários**, recuperando **138.97 GiB** reais. Espaço disponível: **62.43 → 201.40 GiB**. Última entrega Station do servidor/app e as 22 saídas do último conjunto completo Turborama foram preservadas e conferidas por hash. Fontes, SDKs usados pela entrega Station, histórico Git, worktrees com alterações, arquivos privados, ROMs/capas em uso, licenças, clientes, bancos e backups das artes originais foram mantidos. Pastas históricas removidas continuam recuperáveis pelo Git; os arquivos de compilação podem ser reconstruídos.

Pós-limpeza: mesmo PID/DLL, catálogo 32 assinado e 3.685 visíveis, todas as mídias publicadas legíveis, registros online e configurações preservados, timer ativo. Nenhum serviço foi reiniciado. Caminhos intermediários datados removidos não devem ser usados por operadores históricos; usar a entrega atual e suas fontes/receitas. [Inventário da remoção e conferência final](online-plataformas-20261009/limpeza-compilacoes-20261010.json).

## Revisão de endpoints, ações e recebimentos — aplicada em 10/10/2026

A revisão inventariou **14 rotas da API Station, três rotas de prontidão local, 15 rotas administrativas Station, 21 ações v1/social e 12 ações v3**. O recibo lista os caminhos, o código verificado, testes e escopo. As rotas do painel foram examinadas e o self-test administrativo passou; ações comerciais não foram repetidas sobre clientes reais.

| Falha encontrada | Correção aplicada |
|---|---|
| Publicação do índice entre carga inicial e criação do monitor podia deixar o catálogo antigo em uso; recargas paralelas podiam competir. | A versão carregada guarda seu próprio carimbo, valida antes/depois da leitura e serializa recargas; snapshot válido e grants anteriores continuam preservados. |
| Quebras de linha/tabulação em campos de uma linha podiam provocar recusa do parser Android. | Normalização em leitura/importação; a sinopse mantém as quebras permitidas. O índice atual não precisou de alteração. |
| Cancelamento da autenticação do relay antes do upgrade podia terminar como HTTP200 vazio. | Timeout recebe JSON504 com código v1/v3 correspondente; anexos são limpos e cancelamento do cliente não inicia outra resposta. |
| Falhas de autenticação/tempo/arquivo podiam devolver erro vazio ou escapar do tratamento esperado. | Erros de online, prova de pedido e capas seguem status/código JSON finito e Retry-After quando aplicável. |
| O app exibia somente as primeiras 32 salas v3; o cursor compartilhado de pessoas/salas impedia percorrer as demais corretamente. | Paginação opcional no servidor; cursores separados na composição do app, própria sala sempre incluída e presença v3 calculada independentemente da página. |
| Uma falha transitória na consulta podia encerrar o acompanhamento da sala. | O app compilado repete somente consultas/heartbeat após recuo e respeita Retry-After; falhas de identidade/assinatura/acesso continuam terminais. |

**Leitura das salas pelo APK:** enviar `page` de 0 a 40 e `roomPageSize:32` em v3. Sem os campos, o servidor mantém página 0/lote 100 para clientes anteriores. Usar `nextPage` da resposta v3 como próximo cursor de salas; ele é `null` no fim. `totalRooms` conta as salas visíveis antes do recorte. `room` própria independe da página. Em social, manter `nextPeerPage` para Pessoas e `nextRoomPage` para salas antigas; `nextPage` anterior continua disponível. Não sobrescrever esses cursores ao combinar as respostas assinadas. Navegar entre Pessoas/Salas reinicia em página 0, e uma página ainda não recebida mostra consulta em andamento.

**Recuperação das consultas:** HTTP 408/429/500/502/503/504 ou Offline/SocketTimeout/SocketException usam 1, 2, 4 e no máximo 8 segundos de recuo, ampliados por Retry-After até 60 s. POLL_EXISTS usa 1 s. Apenas um poll fica pendente por aparelho. Cancelamento explícito, acesso 401/403, serviço desativado e falhas TLS/JSON/assinatura/identidade encerram a tentativa. Criar, entrar, marcar Pronto, iniciar e outras ações não têm repetição automática. A espera de 10 s e o limite de 15 s já passaram antes da revisão; não foi necessário aumentar o timeout.

**Validação da candidata e da publicação:** dez projetos de testes do servidor passaram, incluindo 1.423 checks v3, 179 checks TLS v3, 62 HTTP v1, 318 convites/códigos, 91 recuperação + 589 observações, 88 falhas de fechamento e regressão da Suite/provas/transferência. TLS v2 separado passou 49 checks. Os 212 fontes Java/D8 foram recompilados; 36 checks do lobby e 200 de perfis passaram sobre a compilação correspondente. HTTPS real conferiu os itens pelo itemId e todos os campos/metadados publicados; 19 capas e 19 downloads tiveram bytes comparados, cabeçalhos e uso único dos grants. Downloads grandes tiveram amostra de 256 KiB por rótulo; isso não verifica o corpo integral.

O sandbox efetivo do novo PID leu **7.670 arquivos de mídia/índice/registros/binário**, negou quatro caminhos protegidos e manteve montagens sem escrita. Timer ativo, scanner status 0 e revisão 31/changed=false/added=0/updated=0. No intervalo da ativação até a conferência às 21:52 UTC, os 1.300 registros Station coletados foram informativos, sem códigos de erro ou exceções; esse intervalo não comprova estabilidade indefinida ou gameplay Android. Túnel compartilhado e demais produtos permaneceram intactos.

Servidor já atualizado **antes** do novo APK, pois o parser anterior não aceitava os campos novos. A produção APK deve consumir esta mesma candidata/receitas, montar com a base privada e certificado original e atualizar os aparelhos preservando dados. Rooms DEX atual: `c87d3efc19ff760015b355a00a45177bb43345ac9bb7a98fb53e61b87cd920e7`; Client DEX permanece `e7207a89517b168cc476e30a823bed3a1b6ad57339e1042f08a4660184543d63`. APK completo, instalação, aparência e gameplay físico ainda não foram comprovados nesta revisão; as dependências nativas das plataformas continuam listadas abaixo.

Cópia anterior publicada antes das mudanças: branch `backup/station-antes-revisao-endpoints-20261010` e tag `backup-station-antes-revisao-endpoints-20261010` nos dois repositórios; servidor 919d31a/app 941b761. A versão de retorno foi qualificada em sombra antes da ativação. Recibos locais em `/mnt/DADOS/station-endpoint-review-20261010/`; não repetir operadores datados. O operador atual já tem marcador applied.json. Salas/conexões são dinâmicas; uma próxima implantação deve consultar o estado atual.

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

## Sinopses na sala — ajuste para a próxima compilação do APK

Títulos, botões e estados são escritos pelo app. `modeTitle` e `instructions` vêm dos perfis assinados do servidor. A **sinopse vem de `metadata.description` do catálogo assinado**, já publicado em `GET /v1/station/catalog?metadata=1`. O app deve procurar pelo `itemId` exato da sala, inclusive para o convidado; não associar pelo nome ou posição da lista.

O fonte `StationRoomsActivity` desta mesma candidata foi ajustado: **Criar sala e Sua sala mostram a sinopse em quatro linhas, com toque para ler o texto inteiro**. Outras salas mostram duas linhas e a íntegra em Ver detalhes. Os limites permanecem compactos perto dos controles; instruções dos modos, controles e posições ficam acessíveis em **Como jogar**. Sem sinopse cadastrada, mostrar “Sinopse ainda não cadastrada.”. O catálogo revisionado é consultado em memória; nenhuma requisição nova por sala.

O catálogo31 contém **3.492 sinopses entre 3.640 jogos visíveis**; os demais precisam de texto cadastrado. Java212/D8 recompilados e selados. **O APK completo assinado e a aparência nos celulares ainda precisam da compilação/instalação no PCAPK**; não há APK instalado por esta alteração. A apresentação das sinopses não exigiu alteração da API ou das regras de admissão; a revisão de endpoints acima foi aplicada posteriormente. Comparar `roomPresentation` no contrato e `evidence/java-build.json` no app.

Cópia anterior publicada nos dois Gits antes deste ajuste: branch `backup/station-antes-sinopse-sala-20261010`, tag `backup-station-antes-sinopse-sala-20261010`; servidor `f478d13`, app `45586f6`. Preservar este mesmo handoff.

## Cadastro do motor real sem nova DLL

O manifesto recebido do APK deve declarar engineId imutável, plataforma, formatos reais, `library`, `runtimeLibrary`, seus hashes SHA-256, `launchReady:true`, `recoveryProtocol:station-stream.v3`, `maximumPlayers` e `controllerProfiles`. Para PS2/Saturn, declarar os layouts acima explicitamente. Cada layout contém `configuration` com `schemaVersion`, `controllerProfile`, `devices`, `coreOptions`. O motor/decoder do app deve implementar essa configuração; nomes de controles por si só não representam sua implementação.

`profileSha256` é SHA-256 do JSON compacto UTF-8, sem BOM/quebra final, Unicode sem escapes e campos na ordem `schemaVersion`, `controllerProfile`, `devices`, `coreOptions`. Exemplos do contrato não são recibos nativos. Não copiar hashes ilustrativos para a produção.

Operador instalado: `/opt/turborama-station-endpoints-20261010-2c0d03447696/library-tools/cadastrar-motor-online-station.py install`, com `--config` da configuração privada efetiva, `--incoming` do manifesto real, `--artifacts` dos binários e `--output` de uma pasta privada nova. Exige administração local, confere ambos os binários e preserva perfis anteriores. `prepare` permite revisão offline; `apply` publica a candidata ainda correspondente ao catálogo. **Recarga em até dez segundos, sem recompilar ou reiniciar o servidor.** Credenciais do app não cadastram motores pela API.

## Testes, capacidade e entrega ao APK

Na publicação histórica PS2/Saturn passaram **1.292 verificações C#**, **172 TLS loopback**, **16 testes Python** de cadastro/importação/permissões e **855 verificações v3 assinadas na sombra**: início, dados nos dois sentidos, retomada e perfis SNES de duas/quatro/cinco pessoas. As 17 políticas e o catálogo/mídia também foram conferidos pelo HTTPS público após ativação. Testes sintéticos não homologam latência, controles ou gameplay Android.

Capacidade configurada: v3 até 100 salas, cinco pessoas conforme modo, janela 256 KiB por direção e replay compartilhado 128 MiB; v1/v2 preservados. Teste sintético de 320 participantes/80 salas de quatro/7.864.320 bytes passou. O orçamento limita admissões; não significa 100 salas de cinco simultâneas ou medição da internet.

O pacote `versions/station-all-platforms-online-20261009` foi recompilado: **212 Java/D8**, alias PS2 BR corrigido e sinopses da sala integradas. Client DEX permanece `e7207a89517b168cc476e30a823bed3a1b6ad57339e1042f08a4660184543d63`; Rooms DEX agora `c87d3efc19ff760015b355a00a45177bb43345ac9bb7a98fb53e61b87cd920e7`. Runtime `81b3daa38fb9c051e1c83646b87df7120f84f31ed8b48fe6b0b362b617b847dc` e motores reais anteriores preservados. Nenhum APK completo foi assinado/instalado nesta sessão; base/keystore continuam no PC do APK. Próxima etapa reúne Dreamcast, GameCube, Wii, Wii U, Switch, PS2 e Saturn, assinatura/instalação e testes físicos.

## Arquivos únicos para comparar e retomar

- `catalogo-cruzado-completo.json`: todos os 3.895 IDs, nomes, plataformas, capas, metadata, artefatos, identidades, perfis e modos preparados; sem caminhos privados de mídia.
- `platform-summary.json`, `profiles-missing.json`: contagens por plataforma e dependências reais.
- `content-identities.json`: 3.895 vínculos/schema2; `profiles-complete.json`: todos os 5.176 perfis. Não substituir por `profiles-new-profiles.json`, que contém só adições históricas.
- `server-integration-contract.json`, `server-prepared-modes.json`: contrato v3 completo, 320 jogos/321 modos, layouts, hashes e receita de cadastro.
- `engines-app.json`: motores reais atuais e políticas do cliente; nenhum engine PS2/Saturn fictício.
- `library-tools/`: scanner, todas as dependências, operador de cadastro e testes correspondentes.
- `endpoint-review-20261010.json`: revisão atual, inventário de rotas/ações, regressões, compilação, ativação e verificação efetiva. `ps2-saturn-servidor-20261010.json`: organização e publicação histórica PS2/Saturn. `ps2-switch-publicados-20261010.json`, `capas-padronizadas-20261010.json`, `checkup-completo-20261010.json`, `server-ready-deployment.json`, `production-applied.json` e `profile-reload-fix.json` preservam as provas históricas. ZIP de 09/10 continua histórico e não representa o catálogo31.

Cópia estável anterior publicada antes das mudanças nos dois Gits: branch `backup/station-antes-online-ps2-saturn-20261010`, tag `backup-station-antes-online-ps2-saturn-20261010`, servidor `26c5302`, app `52ba50d`. Backup privado atual: `/mnt/DADOS/station-ps2-saturn-20261010/backup`. Não repetir `apply.py` nem operadores datados. Consultar o estado dinâmico das salas antes de agir sobre serviços. Os dois arquivos CRLF das entregas R71/R78 anteriores ficaram fora desta alteração.
