# Comunidade R41 publicada — 06/10/2026

Leia RETORNO-COMUNIDADE-STATION-R41-20261006.md. API a2bb176530fd4d2dfa740da7e934fd84d097404e, DLL d181bf97d5b39a334e95144267d6ece3f11d4e659a314d7d16cd2746e1999e13, PID875574; SocialEnabled=true, conversas privadas/pedidos de entrada verificados por três licenças sintéticas no domínio público, 186 checks e WSS com pin. Catálogo14/2212, relay512/1024 e licenças/chaves/outros serviços preservados. Histórico privado até32 e64KiB na resposta. Backup restaurado, nenhuma migration. Retorno Android 5e40f7e confirma R41 instalada no Samsung, hashb6b19321 e dados preservados. POCO, gameplay em dupla, Pessoas/Voltar/correspondência Boogerman–Battletoads e latência externa continuam pendentes. Preservar APK R41; nunca retomar delta R34 sobre essa fonte. Scripts históricos recusam sucessoras; usar retorno/rollback R41.

## Histórico anterior — os blocos abaixo não identificam a publicação atual

# Segundo jogador — correção conciliada com R34 — 05/10/2026

Leia RETORNO-SEGUNDO-JOGADOR-SALAS-STATION-20261005.md. Pronto confirma a sala atual; a entrada precisa ocorrer primeiro. Fonte app f7f0561: três classes alteradas sobre a fonte exata R34,147 entradas Java/dependências compiladas,144 preservadas e213 verificações aprovadas. DEX39864bd1 pronto; montagem e instalação do APK desta correção pendentes. O retorno a8898a0 confirma R34/SHA513dd470 instalado no Samsung; a versão do POCO ainda não foi conferida. Mantidos recuperação de abertura, Voltar/manifesto e design R33. Servidor e4e557a inalterado. Uso imediato: POCO Sair da sala → CódigoTS1 do primeiro telefone → Entrar → dois nomes juntos → ambos Pronto → anfitrião Iniciar. Preservar dados/assinatura/saves e conciliar sucessoras antes de montar; DEX inicial R30 foi substituído. Gameplay em dupla e latência externa baixa continuam pendentes.

## Histórico anterior — consultar o retorno R34 acima

## Retorno final recebido — appR30

Retorno b4a9806 confirma R30 instalado/hash1768b7df em05/10 às18h39, Voltar/criação de sala/Pronto verificados em um aparelho. Usar R30 ou sucessora noPOCO, preservando dados/assinatura/saves; R27 abaixo é histórico. Downloads11be7f3/6f012a7 permanecem fora; doisaparelhos/gameplay e latência externa baixa continuam pendentes.

# Estado vigente — POCO, relay e capacidade — 05/10/2026

Leia [o retorno R12/POCO](RETORNO-SERVIDOR-NETPLAY-INTERNET-STATION-R12-20261005.md). API `e4e557a`, DLL `7ecb6c8d`, PID660598; relay privado publicado no mesmo domínio, 512 salas/1.024 conexões configuradas. Passaram 256 conexões reais, 256 renovações e 512 conexões TLS isoladas, com zero resíduos. **Latência externa alta permanece aberta:** p95 público2.291,82ms versus API0,83ms/Nginxlocal1,07ms. Gameplay de doisAndroid e partidas responsivas para centenas precisam de homologação.

Licença própria POCO vitalícia/um aparelho criada e auditada; código apenas no arquivo privado do operador, ativação até07/10 às17h11Maceió. Retornoapp4fd2231 confirma R27 instalado/hashc1191ce1: preservar assinatura, dados, saves, R26visual e R27salas. Catálogo14/2.212visíveis/50CD, importação, capas e downloads sem capMB/s preservados. LimpezaWS, coldboot, handshake e conflitos entre renovações foram corrigidos. Delta11be7f3/6f012a7 continua fora doAPK27.

Os blocos seguintes são históricos e não identificam aAPI ou instalação atual.

---

## Atualização 05/10: Neo Geo e catálogo completo publicados

Leia [o retorno vigente](RETORNO-SERVIDOR-NEOGEO-VELOCIDADE-20261005.md), [catálogo cruzado](biblioteca-20261005/catalogo-completo.tsv) e [guia de importação](BIBLIOTECA-AUTOMATICA-STATION-20261004.md). Catálogo **9 / 2.162 jogos /189NeoGeo /157N64**,2.119sinopses,43sem fonte e374jogos em subpastas. API931030b mantida; scannercb49214 publicado. TaxaHTTPS até4,37MB/s medida no Linux, sem cap local de1MB/s; o contador não mede velocidade. ClienteR11 conciliado com a fonte MB/s [c8e240a](https://github.com/luziellacerda/TurboElden/tree/c8e240a2c886122e79ca2105c0a719a9217ed7dc/versions/station-neogeo-rate-20261005); R9 instalado e R11 compilado/USBpendente segundo retorno. Não executar rollbacks históricos de04/10 sobre este estado; usar retorno próprio05/10.

## Histórico 04/10: N64 e catálogo automático publicados

Leia [o retorno atual](RETORNO-SERVIDOR-N64-BIBLIOTECA-20261004.md) e [o guia de pastas/metadados](BIBLIOTECA-AUTOMATICA-STATION-20261004.md). API `931030b`, catálogo **8 / 1.973 jogos / 157 N64**, 1.957 sinopses e 313 jogos em subpastas R9. O retorno canônico continua no arquivo de02/10, atualizado no início; os blocos antigos são históricos.

# Integração da TurboramaStation Android no servidor compartilhado

Esta pasta é o ponto de partida para uma ferramenta ou pessoa que vá adicionar o produto TurboramaStation Android ao servidor. Ela explica o que foi observado em 30/09/2026, quais serviços já atendem clientes e como preparar a integração preservando PIX, Suite Windows, EmulationStation Windows, site e conteúdo. É um guia versionado de trabalho; não é um pacote de implantação.

Leia nesta ordem: [regras de trabalho](AGENTS.md), [inventário observado](INVENTARIO-SERVIDOR.md) e [plano de implementação](PLANO-INTEGRACAO.md). O inventário é um retrato datado, não uma afirmação de que uma branch Git corresponde aos binários instalados. Para atualizar a parte observável sem modificar o servidor, execute `bash scripts/inventario-somente-leitura.sh` nesta pasta.

O [handoff técnico único de catálogo, capas e downloads](RETORNO-SERVIDOR-PARA-CLIENTE-RECONSTRUIDO-STATION-20261002.md) é o ponto de comparação atual com o APK. Em 05/10/2026 a API `e4e557a` está publicada com catálogo **revisão14 / 2.212 jogos**, incluindo189NeoGeo,50NeoGeoCD e157N64, importação automática e capas exatas da revista. Relay publicado; capacidade exercitada, latência externa alta ainda aberta. Administração Station e salas online estão publicadas; [retorno do módulo online](RETORNO-SERVIDOR-ONLINE-STATION-20261004.md). O documento contém listas por plataforma, hashes, instruções para atualizar catálogo/cache e as provas ainda necessárias no aparelho.

O [retorno histórico da implementação candidata](RETORNO-IMPLEMENTACAO-CANDIDATA-20260930.md) registra o estado de30/09. O [OpenAPI candidato](openapi-candidato.yaml) também é histórico e parcial; use o handoff técnico atualizado acima para o contrato efetivamente publicado e conferido em produção. Os levantamentos abaixo descrevem30/09 e não substituem esse estado atual.

Para iniciar outra ferramenta neste projeto, informe este diretório como pasta de trabalho e peça: “Leia AGENTS.md, README.md, INVENTARIO-SERVIDOR.md e PLANO-INTEGRACAO.md; execute apenas o inventário de leitura; identifique as diferenças atuais antes de propor código. Preserve os serviços já ativos.”

O [handoff original do Android](https://github.com/luziellacerda/TurboElden/blob/f7887438e41107e73ed32ddeaf634f998b708ded/docs/server/HANDOFF-TURBORAMASTATION-ANDROID-20260930.md) descreve a proposta de compra, ativação, sessão, painel e downloads. O [handoff operacional anterior da Suite](../../HANDOFF-TUTORIAL-COMPLETO-SERVIDOR-TURBORAMA-SUITE-20260903.md) explica o sistema existente. Ambos contêm observações históricas: confirme sempre o estado em execução antes de trabalhar.

Na data deste levantamento, PIX, API Suite, administração Suite e gateway de conteúdo estavam ativos. As verificações HTTP locais em `127.0.0.1:5187/v1/health`, `:5190/health` e `:5191/health` responderam `200`. Isso não comprova o fluxo completo de compra, licença ou jogo. O login remoto do Android ainda não está implementado no APK registrado.

Este guia está na branch de documentação do repositório privado `luziellacerda/Servidor-pix`. Não o copie para um repositório público sem revisar caminhos internos e dados operacionais. Nenhum segredo foi lido ou incluído aqui.
