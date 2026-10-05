# Entrada do segundo jogador — correção preparada — 05/10/2026

Leia RETORNO-SEGUNDO-JOGADOR-SALAS-STATION-20261005.md. R30 oculta outras salas quando há sala própria e o perfil do anfitrião não oferece Entrar. Pronto não executa join. Fonteapp b282b04:3classes,146fontes compiladas/138verificações; DEXef8a2d99 pronto, APK ainda não atualizado. Servidor e4e557a inalterado. Uso imediato: POCO Sair da sala → Código TS1 da sala do primeiro → Entrar → dois nomes → doisProntos → hostIniciar. Preservar R30/assinatura/dados, não reenviarR27. Não afirmar gameplay/latência resolvidos sem aparelho.

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
