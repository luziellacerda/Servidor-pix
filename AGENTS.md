# Cadastro de clientes Station publicado — 06/10/2026

Leia docs/station-android/RETORNO-CADASTRO-CLIENTES-STATION-20261006.md. Fontefe4b631 publicada às15h45Maceió: Códigos Station → Novo cliente e código, cliente novo/existente, venda paga/cortesia/teste e licença adicional para dois aparelhos. Código30min/uso único; confirmação com senha administrativa. PostgreSQL/SQLite restaurados, testes isolados e dois acessos sintéticos independentes na API pública passaram, limpeza confirmada, zero mensagens/compras. ManagementPID910766/helperPID910776; APIa2bb176/PID875574/catálogo14/2212 e APKR41 preservados. Sem migrationPG; tabelaSQLite aditiva station_registrations. Nova página cadastra; orientação antiga somente de busca/Vendas foi substituída. Usar retorno específico da sucessora; gameplay físico/POCO/latência continuam no retornoR41.

## Histórico anterior — consultar o cadastro publicado acima

# Códigos Station no painel — 06/10/2026

Leia docs/station-android/RETORNO-PAINEL-CODIGOS-STATION-20261006.md. Interface17e564a publicada às14h04Maceió: menu Códigos Station, atalho Gerar código do app Station, guia e botões Gerar código/Trocar celular. HTTPS autenticado e hashes dos nove arquivos conferidos; testes sintéticos de emissão/senha/CSRF/troca/cancelamento passaram. Código30min/uso único, um aparelho por licença; novo aparelho simultâneo exige licença própria. Nenhuma licença real alterada, zero WhatsApp, migrations e reinícios. API/comunidadeR41 e APK permanecem os do retorno abaixo.

# Comunidade R41 publicada — 06/10/2026

Leia docs/station-android/RETORNO-COMUNIDADE-STATION-R41-20261006.md. API a2bb176530fd4d2dfa740da7e934fd84d097404e, DLL d181bf97d5b39a334e95144267d6ece3f11d4e659a314d7d16cd2746e1999e13, PID875574; SocialEnabled=true, conversas privadas/pedidos de entrada verificados por três licenças sintéticas no domínio público, 186 checks e WSS com pin. Catálogo14/2212, relay512/1024 e licenças/chaves/outros serviços preservados. Histórico privado até32 e64KiB na resposta. Backup restaurado, nenhuma migration. Retorno Android 5e40f7e confirma R41 instalada no Samsung, hashb6b19321 e dados preservados. POCO, gameplay em dupla, Pessoas/Voltar/correspondência Boogerman–Battletoads e latência externa continuam pendentes. Preservar APK R41; nunca retomar delta R34 sobre essa fonte. Scripts históricos recusam sucessoras; usar retorno/rollback R41.

## Histórico anterior — os blocos abaixo não identificam a publicação atual

# Segundo jogador — correção conciliada com R34 — 05/10/2026

Leia docs/station-android/RETORNO-SEGUNDO-JOGADOR-SALAS-STATION-20261005.md. Pronto confirma a sala atual; a entrada precisa ocorrer primeiro. Fonte app f7f0561: três classes alteradas sobre a fonte exata R34,147 entradas Java/dependências compiladas,144 preservadas e213 verificações aprovadas. DEX39864bd1 pronto; montagem e instalação do APK desta correção pendentes. O retorno a8898a0 confirma R34/SHA513dd470 instalado no Samsung; a versão do POCO ainda não foi conferida. Mantidos recuperação de abertura, Voltar/manifesto e design R33. Servidor e4e557a inalterado. Uso imediato: POCO Sair da sala → CódigoTS1 do primeiro telefone → Entrar → dois nomes juntos → ambos Pronto → anfitrião Iniciar. Preservar dados/assinatura/saves e conciliar sucessoras antes de montar; DEX inicial R30 foi substituído. Gameplay em dupla e latência externa baixa continuam pendentes.

## Histórico anterior — consultar o retorno R34 acima

## Retorno final recebido — appR30

Retorno b4a9806 confirma R30 instalado/hash1768b7df em05/10 às18h39, Voltar/criação de sala/Pronto verificados em um aparelho. Usar R30 ou sucessora noPOCO, preservando dados/assinatura/saves; R27 abaixo é histórico. Downloads11be7f3/6f012a7 permanecem fora; doisaparelhos/gameplay e latência externa baixa continuam pendentes.

# Station atual — 05/10/2026

Leia `docs/station-android/RETORNO-SERVIDOR-NETPLAY-INTERNET-STATION-R12-20261005.md`: APIe4e557a/PID660598, catálogo14/2212, licençaPOCO privada pronta,512salas configuradas e256conexões reais verificadas. API/Nginxlocal p95<1,1ms; WSSexterno p95>2s exige diagnóstico. Preserve appR27/retorno4fd2231 e outros produtos. Capacidade configurada não comprova gameplay homologado. Scripts históricos recusam versão posterior.

# Servidor-pix em servidor compartilhado

Antes de trabalhar na integração da TurboramaStation Android, leia integralmente `docs/station-android/AGENTS.md`, `docs/station-android/README.md`, `docs/station-android/INVENTARIO-SERVIDOR.md` e `docs/station-android/PLANO-INTEGRACAO.md`. O handoff original do aplicativo está vinculado no README. Execute `bash docs/station-android/scripts/inventario-somente-leitura.sh` para conferir o estado atual; o inventário do guia é datado.

O servidor atende outros produtos em produção. Preserve PIX, Suite Windows, EmulationStation Windows, site, WhatsApp, gateway, painel, bancos, dados dos clientes e demais serviços. Integre o Android de forma aditiva e isolada; não trate a leitura do guia como autorização para implantação. Não altere unidades systemd, Nginx, Cloudflare, firewall, portas, `/opt`, `/etc`, `/var/lib`, bancos, worktrees existentes ou APK em consequência deste guia. Confirme separadamente o alvo e o plano de retorno antes de qualquer mudança de produção.

Mantenha segredos e dados pessoais fora dos relatórios e commits. Use testes e contas sintéticos. Se o estado observado divergir do guia, investigue antes de codificar ou implantar.
