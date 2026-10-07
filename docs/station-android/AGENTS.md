# Auditoria adicional Neo Geo cartucho/CD — R65 — 07/10/2026

Leia [PEDIDO-AUDITORIA-NEOGEO-CARTUCHO-CD-R65-20261007.md](PEDIDO-AUDITORIA-NEOGEO-CARTUCHO-CD-R65-20261007.md). APP → SERVIDOR, pedido de auditoria, sem implantação. Fonte app `e99f6fa705f4629085c426ffb7cf02b4fc624239`, branch `fix/station-neogeo-cd-r65-20261007`; R65 APK1858459b compilado/reproduzido, não instalado. CD tinha helper ausente no APK anterior, agora integrado. TSV14:189cartuchos/50CD,13nomes sem registro MAME0289 (seis KOF) e dois itens de outro hardware; conferir produção real e membros/CRC antes de mapear. KOF98 padrão registrado chegou ao motor, imagem corrompida em captura curta; causa ainda não comprovada/USB ausente. Responder NG-01–NG-07, preservando IDs/licenças/saves e demais serviços. O pedido Q01–Q08 de retomada online abaixo continua aberto e independente. Não declarar todos os jogos corrigidos ou R65 instalada.

## Histórico e pedido de retomada preservados

# Pedido atual: aguardar a conexão e retomar a partida — 07/10/2026

Leia [PEDIDO-QUEDA-RETOMADA-PARTIDA-STATION-20261007.md](PEDIDO-QUEDA-RETOMADA-PARTIDA-STATION-20261007.md). **APP → SERVIDOR, pedido novo; não é retorno nem implantação.** Fonte app R64 `8812bacf8154f25fd7739f95a2db0729f36e4c72`: controles online adaptados e diagnóstico, compilados/reproduzidos, ainda não instalados. R63 joga online segundo o mantenedor, depois houve queda observada às12:24:07Z; causa exata não comprovada. Implementar sessão recuperável/espera sem encerramento por timeout, loading Aguardando conexão e retomada sincronizada. Responder Q01–Q08 e matriz de perdas com contrato, fontes/runtime e compatibilidade; não sugerir apenas reconectar o WebSocket ou apagar os timers. Preservar autenticação, dados, Binder, saída e todos os outros produtos.

## Histórico preservado

# R63 instalada nos dois aparelhos — recibo Android07/10/2026

Leia [RECIBO-APP-R63-DOIS-APARELHOS-20261007.md](RECIBO-APP-R63-DOIS-APARELHOS-20261007.md). APP → SERVIDOR. Samsung A56 e Motorola Edge30 atualizados, mesmo APKd9a35602 conferido integralmente; LoginActivity voltou ao catálogo sem nova licença. Fonte/documentação `8613d88d4f553e72e5fedcd1d3ea470010301734`. Falta de espaço Samsung resolvida; textos anteriores são históricos. Gameplay em dupla permanece pendente, conferências físicas a cargo do mantenedor. Criar sala nova com dois aplicativos R63; sem novo deploy Linux.

## Histórico preservado

# R63 entregue — convite curto e senha automática integrados

Leia [ENTREGA-APP-R63-CONVITE-SENHA-INTEGRADOS-20261006.md](ENTREGA-APP-R63-CONVITE-SENHA-INTEGRADOS-20261006.md). **APP → SERVIDOR**, não resposta nova do servidor. Fonte TurboElden `c7ac337e31c9a5608407c5502c84781f1bb2df47`, branch `fix/station-r63-auto-access-20261006`; R62 conciliada com retorno8d9c670/candidato1e0f862. APKd9a35602, runtime899e3527,302testesWindows e restauraçãoDEXidêntica. Instalação aguarda saída segura do jogoMotorola; SamsungR62/MotorolaR58. Atualizarambos para R63; gameplay em dupla pendente. Não restaurar Activities antigas nem implantar Linux por esta entrega.

## Histórico preservado

# Cliente R62 entregue; usar fonte atual para análise — 06/10/2026

Leia [ENTREGA-APP-R62-INTEGRADA-PARA-SERVIDOR-20261006.md](ENTREGA-APP-R62-INTEGRADA-PARA-SERVIDOR-20261006.md). Entrega **APP → SERVIDOR**, não novo retorno do servidor. Fonte TurboElden `087b6823814d1ec6fc3b925dba8045d9b5628f02`, branch `fix/station-r62-integrated-20261006`. Retorno7c6e167/f8b019d6 integrado em DEX/APK real, R62 instalada/hash114dba8a no A56. Preservados Binder/saída/runtime e nativo R57. Capas/fluxo de criação/botões/painel único atualizados. Fonte = R55 + R57 + nove Java R62;161 hashes e rebuildDEX idêntico. Conferência física a cargo do mantenedor, dois aparelhos ainda não homologados. MotorolaR58 precisa sucessora após novo retorno; não restaurar Activity antiga. Nenhum deploy Linux.

## Histórico preservado

# Retorno R55 concluído considerando a sucessora R57 — 06/10/2026

Leia `RETORNO-ANALISE-APP-R55-STATION-20261006.md`. Base funcional recebida9d3d45f, adendo visual8980cd4; implementação atualf8b019d6 em `versions/station-relay-readiness-r57-20261006` do app. Preservados canal Binder/saída R54 e layout R57: Criar sala, barra fina, capas e faixa INSTALADO. Os snapshots recebidos permanecem intactos.

Composição dos157 Java atuais coincide com o recibo de produção. O delta conserva155 arquivos, altera dois e adiciona um;158 fontes compilaram Java8/API34 em api-check-only. Provas39TCP/TLS/relay e255salas se aplicam aos mesmos componentes, sem repetição por layout. Patch e recusas R41/R55/api-check-only passaram. Não há novo DEX/APK compilado ou instalado, nem gameplay físico comprovado.

Sem alteração de servidor necessária para este delta. APIa2bb176/PID875574, management910766 e helper910776 preservados. No PC usar as novas receitas R57 e o APKbase e6159fa3/certificadooriginal, conferindo todos os outros módulos. Não usar o empacotador R41 ou R55 sobre a sucessora. Testar dois aparelhos, confirmação, inputs, saída e retorno; controles online próprios, latência externa e aquecimento medido continuam pendentes.

## Histórico anterior

# Adendo do app: R57 visual instalada, revisão funcional R55 mantida

Leia [ATUALIZACAO-VISUAL-APP-R57-PARA-REVISAO-20261006.md](ATUALIZACAO-VISUAL-APP-R57-PARA-REVISAO-20261006.md). Fonte atual: TurboElden `8980cd422d63068299b5e9946c120f81a9c94f29`, snapshot R55 + overlay R57. Layout de Criar sala e barra preta compactados; faixa INSTALADO e capas preservadas. Canal Binder/fechamento/runtime/protocolo permanecem R55. Ao responder R55-01 a R55-08, considerar o overlay R57 se alterar `StationRoomsActivity`. Candidato de prontidão d1b535c não integrado; não declarar gameplay corrigido nem implantar por esta leitura.

## Pedido anterior e histórico preservados

# Novo pedido: analisar o APP R55 atual — 06/10/2026

Leia [PEDIDO-ANALISE-APP-R55-STATION-20261006.md](PEDIDO-ANALISE-APP-R55-STATION-20261006.md). **APP → SERVIDOR, pedido do mantenedor; não é retorno nem implantação.** A fonte atual do app foi publicada em TurboElden, branch `review/station-r55-server-20261006`, commit `9d3d45f048aa44bb2ee9c41f567e985901628daa`. APK R55 instalado/hash4c8de4f8; R41 abaixo é histórico. Conciliar o candidato de prontidão d1b535c com `StationSessionChannel`, fechamento idempotente e HUD da R54/R55. Não copiar a Activity da R41 nem executar seu empacotamento sobre R55. Responder R55-01 a R55-08 em `RETORNO-ANALISE-APP-R55-STATION-20261006.md`, citando a fonte exata. Gameplay em dupla, retorno online completo e controles próprios continuam pendentes; não implantar por consequência da leitura.

## Histórico anterior — referências R41 abaixo não identificam o APK atual

# Cadastro de clientes Station publicado — 06/10/2026

Leia RETORNO-CADASTRO-CLIENTES-STATION-20261006.md. Fontefe4b631 publicada às15h45Maceió: Códigos Station → Novo cliente e código, cliente novo/existente, venda paga/cortesia/teste e licença adicional para dois aparelhos. Código30min/uso único; confirmação com senha administrativa. PostgreSQL/SQLite restaurados, testes isolados e dois acessos sintéticos independentes na API pública passaram, limpeza confirmada, zero mensagens/compras. ManagementPID910766/helperPID910776; APIa2bb176/PID875574/catálogo14/2212 e APKR41 preservados. Sem migrationPG; tabelaSQLite aditiva station_registrations. Nova página cadastra; orientação antiga somente de busca/Vendas foi substituída. Usar retorno específico da sucessora; gameplay físico/POCO/latência continuam no retornoR41.

## Histórico anterior — consultar o cadastro publicado acima

# Códigos Station no painel — 06/10/2026

Leia RETORNO-PAINEL-CODIGOS-STATION-20261006.md. Interface17e564a publicada às14h04Maceió: menu Códigos Station, atalho Gerar código do app Station, guia e botões Gerar código/Trocar celular. HTTPS autenticado e hashes dos nove arquivos conferidos; testes sintéticos de emissão/senha/CSRF/troca/cancelamento passaram. Código30min/uso único, um aparelho por licença; novo aparelho simultâneo exige licença própria. Nenhuma licença real alterada, zero WhatsApp, migrations e reinícios. API/comunidadeR41 e APK permanecem os do retorno abaixo.

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

# Instruções para integrar a TurboramaStation Android neste servidor

Você está no servidor compartilhado `lz-servidor`. Sua tarefa é acrescentar uma licença e um cliente Android próprios, aproveitando comércio, PostgreSQL, autenticação, painel e conteúdo existentes. Leia `README.md`, `INVENTARIO-SERVIDOR.md` e `PLANO-INTEGRACAO.md` antes de propor alterações. Leia também o handoff Android vinculado no README. Execute `bash scripts/inventario-somente-leitura.sh` para obter o estado atual; o inventário datado pode envelhecer.

## Limites do trabalho

- PIX, Suite Windows, EmulationStation Windows, site, WhatsApp, gateway, painel, banco e serviços de outros projetos atendem usuários. Preserve seus fluxos, dados, endpoints, nomes, contratos criptográficos e configurações existentes. A integração Android deve ser aditiva e isolada por produto, aplicação, rota e feature flag.
- Não use `main`, um worktree ou o `WorkingDirectory` de systemd como prova da versão instalada. Registre o `ExecStart` efetivo, os drop-ins e o hash do binário em execução. Os quatro serviços principais atualmente usam diretórios de release diferentes.
- Há mudanças locais não commitadas em worktrees do Servidor-pix. Preserve-as. Faça desenvolvimento em worktree/branch próprios, em armazenamento com espaço, após escolher conscientemente a base compatível com os binários instalados.
- Não altere unidades systemd, drop-ins, Nginx, Cloudflare, firewall, portas, `/opt`, `/etc`, `/var/lib`, dados PostgreSQL, MariaDB, Redis, checkout de produção ou APK por causa da leitura deste handoff. Uma tarefa posterior de implantação precisa indicar o artefato, a migration, o alvo exato e o retorno possível; esses dados devem ser conferidos antes de executar a mudança.
- Não desinstale o APK, limpe dados do telefone, troque certificado de assinatura, modifique emuladores ou retome o trabalho de PS2 por consequência da integração de login. Preserve jogos, saves, capas e configuração do aplicativo.
- Não imprima nem copie DSNs completos, arquivos `.env`, senhas, peppers, tokens, códigos de ativação, chaves privadas, dados pessoais de compradores ou URLs privadas de jogos. Use dados sintéticos nos testes e relatórios.

## Antes de codificar

1. Concilie código, binários efetivos, rotas publicadas e ledger real `suite.schema_migrations`. O diretório de código local contém arquivos até `027`; isso não prova aplicação da migration `027` em produção.
2. Consulte o responsável comercial para definir SKU/plano Android, preço, prazo, quantidade de aparelhos, entrega/reemissão do código e política de transferência. Não reaproveite por suposição a licença vitalícia Windows.
3. Escreva contrato Android versionado e vetores de assinatura interoperáveis .NET/Android. Separe produto, aplicação, licença, aparelho, ação, sessão e desafio nos bytes assinados.
4. Escolha um worktree novo a partir da revisão correta, mantendo os atuais intactos. Desenvolva e teste com banco isolado, licença sintética e rotas Android inicialmente desativadas.

## Para considerar uma implantação pronta

Exija backup restaurável, diff revisado, migrations somente aditivas, testes de regressão PIX/Suite/ES/conteúdo e testes Android de ativação, sessão, revogação, transferência, catálogo e download. Registre hashes, serviço exato, saúde antes/depois e plano de retorno. Uma falha em outro serviço é um limite para a implantação, não um motivo para desativar validações existentes.

Atualize os documentos desta pasta quando descobrir evidência nova. Separe sempre fatos observados, proposta técnica e decisões ainda pendentes.
