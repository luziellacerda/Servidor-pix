# Instruções para integrar a TurboramaStation Android neste servidor

A tarefa atual é `HANDOFF-APP-IMPLEMENTAR-STATION-20261002.md`: implementar no **aplicativo Android** o nome do consumidor, a abertura da biblioteca sem senha repetida, o cache de catálogo/capas e o cliente de concessão de um uso. Não altere o servidor. Não abra catálogo nem download na `5192`. Não reinicie `5190`, `5192`, admin nem PIX. Grave o retorno em `RETORNO-APP-IMPLEMENTAR-STATION-20261002.md`. Pepper, DSN, token, chave privada, senha, device id, ponto de montagem e URL de jogo continuam de fora.

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
