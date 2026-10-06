# R55 analisada; prontidão conciliada — 06/10/2026

Leia `RETORNO-ANALISE-APP-R55-STATION-20261006.md`. Fonte atual do app:9d3d45f; implementação conciliada:8e62ed2; retorno do servidor:2fba03b; cópia no app:717b211. O delta sobre R55 preserva StationSessionChannel nos dois sentidos, proprietário e saída idempotente R54, StationExitPanel e HUD atuais.

346 arquivos conferidos; 154 fontes Java preservados, dois alterados e um novo. Passaram 39 verificações TCP/TLS/relay e 255 das regras de salas. 157 fontes Java8/API34 compilaram somente em api-check-only. Não há novo DEX/APK compilado ou instalado, nem gameplay físico comprovado.

Sem alteração de servidor necessária para este delta. API a2bb176/PID875574, management910766 e helper910776 mantidos. O hash da DLL foi herdado da publicação protegida às20:59Z; não houve nova leitura root nesta revisão. Produção apenas inspecionada.

No PC usar as receitas novas sobre o APK R55/hash4c8de4f8, com certificado e dependências originais; conservar todos os demais módulos e dados. Não aplicar empacotamento R41. Conferir ambas as versões, logs/geração/etapas, gameplay, saída e retorno. Controles online próprios, aquecimento medido e latência externa continuam pendentes.

## Histórico anterior

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
