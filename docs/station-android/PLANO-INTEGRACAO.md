# Convites curtos e palavra passe automática — 06/10/2026

Leia `docs/station-android/RETORNO-CONVITE-CURTO-SENHA-AUTOMATICA-STATION-20261006.md`. APIa3e83d96/DLL5fff55c1/PID970425 publicada20h04Maceió. Convite8caracteres, resolução autenticada/assinada sem senha ou entrada implícita; quatro motores no registro, dois originais e dois -autopass1. Runtime899e3527 arm64/API26/16KiB compilado, cores/opções/controles preservados. Backup restaurado, provas sombra/HTTPS/relay e limpeza passaram; catálogo14/2212, licenças, chaves, serviços e mídias preservados. Sem migration/mensagens.

App1e0f862 em versions/station-auto-room-access-r57-20261006 inclui R55+visualR57+prontidãof8. Senha privada usada automaticamente só no cliente Android tipado, NICK antes de PASSWORD e verificador original preservado. 318códigos,278salas/convitesJVM,66nativo,39TCP/TLS/relay,158JavaAPI34 passaram. APK/DEX novo, assinatura/instalação e gameplay físico pendentes no PC; usar receitas novas sobre APKR57/e6159fa3/cert7b16 e atualizar ambos. Não executar receitas R41/R55 nem declarar o diálogo do APK antigo corrigido pelo servidor.

## Histórico anterior

# Cadastro de clientes Station publicado — 06/10/2026

Leia RETORNO-CADASTRO-CLIENTES-STATION-20261006.md. Fontefe4b631 publicada às15h45Maceió: Códigos Station → Novo cliente e código, cliente novo/existente, venda paga/cortesia/teste e licença adicional para dois aparelhos. Código30min/uso único; confirmação com senha administrativa. PostgreSQL/SQLite restaurados, testes isolados e dois acessos sintéticos independentes na API pública passaram, limpeza confirmada, zero mensagens/compras. ManagementPID910766/helperPID910776; APIa2bb176/PID875574/catálogo14/2212 e APKR41 preservados. Sem migrationPG; tabelaSQLite aditiva station_registrations. Nova página cadastra; orientação antiga somente de busca/Vendas foi substituída. Usar retorno específico da sucessora; gameplay físico/POCO/latência continuam no retornoR41.

## Histórico anterior — consultar o cadastro publicado acima

# Códigos Station no painel — 06/10/2026

Leia RETORNO-PAINEL-CODIGOS-STATION-20261006.md. Interface17e564a publicada às14h04Maceió: menu Códigos Station, atalho Gerar código do app Station, guia e botões Gerar código/Trocar celular. HTTPS autenticado e hashes dos nove arquivos conferidos; testes sintéticos de emissão/senha/CSRF/troca/cancelamento passaram. Código30min/uso único, um aparelho por licença; novo aparelho simultâneo exige licença própria. Nenhuma licença real alterada, zero WhatsApp, migrations e reinícios. API/comunidadeR41 e APK permanecem os do retorno abaixo.

# Comunidade R41 publicada — 06/10/2026

Leia RETORNO-COMUNIDADE-STATION-R41-20261006.md. API a2bb176530fd4d2dfa740da7e934fd84d097404e, DLL d181bf97d5b39a334e95144267d6ece3f11d4e659a314d7d16cd2746e1999e13, PID875574; SocialEnabled=true, conversas privadas/pedidos de entrada verificados por três licenças sintéticas no domínio público, 186 checks e WSS com pin. Catálogo14/2212, relay512/1024 e licenças/chaves/outros serviços preservados. Histórico privado até32 e64KiB na resposta. Backup restaurado, nenhuma migration. Retorno Android 5e40f7e confirma R41 instalada no Samsung, hashb6b19321 e dados preservados. POCO, gameplay em dupla, Pessoas/Voltar/correspondência Boogerman–Battletoads e latência externa continuam pendentes. Preservar APK R41; nunca retomar delta R34 sobre essa fonte. Scripts históricos recusam sucessoras; usar retorno/rollback R41.

## Histórico anterior — os blocos abaixo não identificam a publicação atual

# Plano de implementação da TurboramaStation Android no servidor

Objetivo: acrescentar ao comércio existente um produto Android próprio, com ativação individual, vínculo de aparelho, sessão, painel, catálogo e downloads, preservando o comportamento de PIX, Suite Windows, EmulationStation Windows, site e conteúdo. Este é um plano técnico; nenhum endpoint Android, migration ou mudança de produção foi aplicado por este documento.

## Fluxo esperado

1. O backend confirma o pagamento e associa pedido, item e comprador. Um evento repetido preserva a mesma entrega.
2. Uma licença Android própria é criada sob um produto/SKU decidido pelo responsável comercial. O comprador recebe um código de ativação individual de uso único, com expiração e reemissão controlada.
3. O app Android gera chave RSA no Android Keystore, prova posse em desafio assinado e vincula o aparelho de forma transacional. O código deixa de ser necessário nas aberturas seguintes.
4. O servidor emite sessões curtas e renováveis apenas para aquela licença, produto, aplicação e aparelho. Bloqueio ou transferência revogam as sessões certas, sem atingir a licença Windows do mesmo comprador.
5. O painel exibe comprador, pedido, licença, aparelho e sessão Android. A rota autenticada de perfil entrega somente o `displayName` necessário para “Bem-vindo, {displayName}”.
6. O catálogo Android consulta seus próprios itens e a autorização de download verifica licença, aparelho e item; o gateway emite concessão curta. A conclusão do download/extração é registrada separadamente da emissão do link.

## Contrato e isolamento

Use um módulo Android com rotas versionadas `/v1/station/…`, inicialmente atrás de feature flag desativada. O [handoff original](https://github.com/luziellacerda/TurboElden/blob/f7887438e41107e73ed32ddeaf634f998b708ded/docs/server/HANDOFF-TURBORAMASTATION-ANDROID-20260930.md) propõe `activations/challenge`, `activations/complete`, `challenges`, `sessions`, `me`, `catalog` e `downloads/authorize`. Trate esses nomes como proposta até fechar OpenAPI e testes de cliente.

Separe explicitamente `TURBORAMA_STATION_ANDROID` das identidades `TURBORAMA_SUITE` e EmulationStation Windows. Não reutilize o cabeçalho `X-TurboRama-Client: EMULATIONSTATION` nem mude a serialização canônica já assinada por clientes existentes. Os bytes assinados da nova versão devem ligar produto, aplicação, licença, dispositivo, sessão, ação e desafio; teste vetores idênticos em .NET e Android. O código de ativação não é senha universal nem chave do painel.

O login Android instalado ainda usa decisão local e marcador no Keystore. O servidor novo não deve interpretar esse marcador como licença comercial. A equipe Android precisa atualizar o cliente de forma assíncrona, preservar assinatura do APK, dados, saves, emuladores e retorno dos jogos. Durante jogo, a renovação deve ter um coordenador único e uma política explícita para prazo offline e revogação.

## Ordem de execução

1. **Fixar a linha de base.** Rodar o inventário de leitura, salvar hashes e caminhos efetivos dos quatro serviços, identificar as versões de fonte correspondentes e ler o ledger do banco por acesso autorizado. As releases atuais são diferentes entre si. Preservar as mudanças locais dos worktrees existentes.
2. **Fechar decisões comerciais.** Registrar SKU, plano/preço, prazo, limite por licença, canal de entrega do código, reemissão, transferência e o vínculo do cadastro comprador. A decisão atual é R$ 99,90, sem expiração, sem limite de compradores e com **um aparelho ativo por licença**. Não converter vendas Windows em Android por suposição.
3. **Criar ambiente isolado.** Escolher a base de código que corresponde à produção real, criar branch/worktree próprios em `/mnt/DADOS`, usar banco e licenças sintéticas e manter os serviços atuais intactos. Validar espaço e dependências antes de compilar.
4. **Publicar o contrato de desenvolvimento.** Escrever OpenAPI, erros, TTLs, paginação e vetores de assinatura/codificação. Fixar chave pública e `keyId` de teste sem embutir segredos no repositório ou APK.
5. **Implementar backend aditivo.** Criar produto e sessão Android com migrations somente expansivas, constraints de produto/SKU explícitas, transações contra dupla ativação, índices e grants mínimos. A numeração da migration virá do ledger real, não do número máximo de um worktree.
6. **Integrar administração e conteúdo.** Reusar venda, auditoria e cadastro por relacionamento confiável pedido → comprador; adicionar filtro Android e ações com autorização, CSRF e confirmação existentes. Integrar concessão de download sem forjar sessão Suite e sem publicar URLs privadas de jogo.
7. **Testar regressões e cliente.** Testar compra repetida, cancelamento fora de ordem, código expirado/usado, prova adulterada, duas ativações concorrentes, sessão, revogação, transferência, outro produto, catálogo, download, retorno de jogo e ausência de impacto em PIX/Suite/ES. Usar somente dados e pagamentos sintéticos.
8. **Homologar separado.** Iniciar os candidatos somente em ambiente/porta isolados e não publicados; validar esquema e backup/restauração. A rota de produção continua desativada até cumprir os critérios.
9. **Implantar com precisão.** Empacotar release imutável e hash, aplicar migration expansiva com backup verificado e janela apropriada, trocar somente binários/serviços envolvidos e checar saúde e regressão. Conservar release anterior e rollback de serviço compatível com schema novo. Não depender de down migration destrutiva.

## O que pode começar já

Inventário read-only, mapa dos binários e worktrees, revisão de código, OpenAPI preliminar, vetores de assinatura, testes sintéticos e projeto de migrations podem ser feitos em uma branch isolada. Essas etapas não exigem alteração dos sistemas produtivos. O SDK .NET 8 foi encontrado neste servidor; compilações devem usar `/mnt/DADOS` devido ao pouco espaço livre na raiz.

As mudanças de comércio e implantação dependem das decisões do produto e da conferência do banco real. O handoff de 30/09/2026 é proposta, não prova de que o backend Android já exista.

## Critérios para aceitar a integração

| Área | Verificação necessária |
| --- | --- |
| Sistemas atuais | PIX, Suite, ES, painel, gateway, site e notificações mantêm contratos e estados esperados |
| Comercial | Compra idempotente; Android recebe licença própria; R$ 99,90 sem expiração, sem limite de compradores e um aparelho ativo por licença |
| Ativação | Código único, expiração e prova RSA; concorrência segura para um aparelho ativo por licença |
| Sessão | Renovação, bloqueio, transferência e retorno de jogo sem duplicar login ou processo |
| Painel | Comprador correto, ações de alvo exato, autorização administrativa e auditoria |
| Conteúdo | Catálogo por produto; autorização por item; grant curto; download e extração rastreados |
| Operação | Regressões aprovadas, release/backup identificados, rollback ensaiado, segredos fora de logs e Git |

O estado `failed` observado no monitor de conteúdo precede este trabalho. Registre-o na linha de base; não atribua sua causa à integração Android sem diagnóstico.
