# Handoff — retomada pelo túnel, IP público atualizado e aceite real da Suite

Data: **08/09/2026**. Horários desta entrega em UTC e America/Fortaleza (UTC−3).
Autor do levantamento: Codex no Windows do proprietário.
Estado: **documentação de continuidade; nenhum reparo ou deploy executado nesta entrega**.

## 1. Pedido e escopo desta retomada

O proprietário esclareceu que não possui IP público fixo, que o acesso externo é publicado por túnel, que já atualizou o IP e que o IPv4 público atual é **`187.21.51.38`**. Em seguida, solicitou explicitamente um **novo handoff**.

Este endereço é informação fornecida pelo proprietário: não foi feita nesta etapa uma medição independente da WAN do servidor, nem confirmado qual campo/configuração ele alterou ou o instante da alteração. Não presumir que seja também o IP de saída deste PC. Não foi feita varredura ou tentativa de acesso direto ao endereço informado.

Este novo arquivo é uma exceção solicitada pelo proprietário à orientação anterior de não criar novos handoffs. Ele concentra a retomada após esse esclarecimento; não apaga o incidente original nem o retorno Linux. Registrar as próximas evidências e o retorno desta retomada na seção 8 **deste arquivo**, por commits, sem abrir outra cadeia de documentos salvo novo pedido explícito.

Objetivo: localizar a camada que ainda impede o login real e restabelecer o funcionamento preservando túnel, domínios, licenças, dados, chaves, contratos e demais consumidores. Muitos clientes utilizam a infraestrutura; uma configuração manual em um único PC não representa solução geral.

O pedido atual autoriza criar/publicar documentação. Não equivale a autorizar neste turno mudanças em produção, reinício de equipamentos, compras de plano Cloudflare, exposição da origem ou alterações no cliente. O executor deve confirmar sua autorização operacional antes de aplicar qualquer delta.

## 2. Arquitetura que deve ser mantida

O cliente acessa **`https://app.lzgames.com.br/`**, conforme a autoridade pública incorporada ao produto. Ele não consulta PostgreSQL diretamente nem precisa conhecer o IP público do servidor.

Percurso lógico da requisição: **programa → Cloudflare → túnel `lz-fix` → Nginx/API → armazenamento**.

- O `cloudflared` estabelece conexões de saída com a Cloudflare. A publicação por Tunnel não depende de IP público fixo: após mudança da WAN e restabelecimento da conexão, o domínio pode continuar associado ao mesmo túnel, sem inserir o novo IP público no cliente.
- O DNS público associado ao túnel não deve ser substituído por um registro A para `187.21.51.38` como tentativa de reparo. Não trocar o domínio por esse endereço, abrir portas ou contornar Cloudflare/TLS.
- Troca do IP **interno** só exige ajustar um destino quando ele realmente referenciar o endereço antigo. No levantamento Linux disponível, os destinos do túnel são locais, em loopback; a troca do IP LAN não exige substituir `127.0.0.1` pelo IP público ou privado novo.
- A conexão **cliente → Cloudflare** é diferente da conexão **cloudflared → Cloudflare**. A família IPv4/IPv6 de um trecho não determina a do outro. Alterar `--edge-ip-version` no servidor não força o EXE remoto a escolher IPv4.

Última configuração documentada, não uma nova leitura administrativa feita neste Windows:

| Componente | Destino/estado documentado | Preservação |
| --- | --- | --- |
| Túnel `lz-fix` | Um conector, cloudflared 2026.8.3, quatro conexões; configuração remota v15 | Conferir configuração efetivamente carregada, sem recriar túnel ou credenciais |
| `app.lzgames.com.br` | CNAME proxied para o túnel; ingress `http://127.0.0.1:80` | Portal e roteamento Nginx por caminho |
| `pix.lzgames.com.br` e `painelpix.lzgames.com.br` | Ingress `http://127.0.0.1:5187` | PIX, BFF e proteção do painel |
| Suite/ES | API local 5190, alcançada pelas rotas do Nginx | Protocolo, assinaturas, identidade e isolamento |
| Conteúdo | Controle na API 5190; arquivos no gateway local 5191 | Grants, integridade, expiração e Range/retomada |
| Administração/armazenamento | Socket administrativo local; armazenamentos distintos por sistema | Não expor PostgreSQL, socket ou serviços internos |

Fonte operacional mais recente encontrada: [manual do servidor, commit f1aaba82](https://github.com/luziellacerda/Servidor-pix/blob/f1aaba82aea09b2bf48063d32f16375c0e1cc793/HANDOFF-FUNCOES-E-OPERACAO-SERVIDOR-PIX-SUITE-20260908.md), publicado às 09:20:42 locais. Ele acrescenta documentação de funções/operação; **não é um novo reparo de conectividade**. O retorno do incidente continua sendo o registrado em d4918a3d.

## 3. Novas sondas Windows após o esclarecimento do proprietário

Janela do lote: aproximadamente **12:03:02–12:03:09 UTC / 09:03:02–09:03:09 UTC−3**, em 08/09/2026. A saída registrou início `2026-09-08T09:03:02.8695081-03:00`; não foram registrados timestamps individuais para cada chamada.

Ferramenta: `curl.exe`, GET, sem credenciais, sem redirecionamento automático, `--noproxy '*'`, `--connect-timeout 4`, `--max-time 7`, validação TLS padrão, corpo descartado em `NUL`. Não foi usado `-k`, override DNS, edição de hosts, login, OTP ou grant. A versão de curl não foi registrada neste lote.

| Teste | URL | HTTP / saída curl | IP remoto conectado | TCP / TLS / total, segundos |
| --- | --- | --- | --- | --- |
| Suite automático, sem `-4`/`-6` | `https://app.lzgames.com.br/v1/suite/challenges` | 405 / 0 | `104.21.3.166` | 0.339910 / 0.452172 / 0.566137 |
| Suite IPv4, `-4` | Mesma URL | 405 / 0 | `172.67.130.243` | 0.189890 / 0.299400 / 0.400105 |
| Suite IPv6, `-6` | Mesma URL | 000 / 28, timeout | Nenhum conectado | 0 / 0 / 4.020469 |
| PIX IPv4, `-4` | `https://pix.lzgames.com.br/v1/health` | 200 / 0 | `104.21.3.166` | 0.369203 / 0.485965 / 0.601409 |

`time_appconnect` é o tempo acumulado até completar TLS, não a duração isolada do handshake. `http=000` é ausência de resposta HTTP no curl, não um status enviado pelo servidor. Os dois IPv4 remotos observados são destinos da borda Cloudflare, não o IP público `187.21.51.38` informado para a WAN.

### 3.1 Conclusões permitidas

1. O domínio Suite respondeu por IPv4, inclusive no modo automático deste curl. Portanto, não existe evidência neste lote de indisponibilidade total desse caminho público.
2. GET 405 é compatível com o contrato anterior de `/v1/suite/challenges`, que exige POST. Não é um login aprovado, nem prova de consulta ao banco neste lote.
3. A sonda IPv6 desta origem não completou conexão no limite de quatro segundos. É reprodução de timeout, não localização do salto defeituoso nem prova de falha em toda a Internet.
4. O endpoint público de saúde PIX respondeu 200. Como o corpo foi descartado, este lote não validou JSON/`ready`, integridade do estado nem pagamento.
5. Os testes foram feitos pelo domínio, sem usar o IP público informado como destino. A necessidade de IP público fixo **não** foi demonstrada como causa.

### 3.2 Limites que não podem ser omitidos

- Não houve tentativa autenticada no EXE neste lote. O problema original não pode ser marcado como resolvido por estes quatro resultados.
- O probe .NET antigo, descrito no incidente anterior, expirou com o handler normal, enquanto uma variante IPv4 respondeu. O sucesso automático do **curl agora não comprova sucesso do SocketsHttpHandler nem do EXE**; bibliotecas e seleção de endereços podem diferir. Não houve nova sonda .NET equivalente nesta entrega.
- Não foram recolhidos headers, CF-RAY, X-Correlation-ID ou corpos neste lote. Não inventar identificadores. O operador pode procurar a janela aproximada nos logs, mas essas entradas continuam sendo sondas, não login do programa.
- TLS padrão do curl foi aceito nas respostas IPv4; o pin SPKI específico do produto não foi revalidado por esse teste. A verificação de pin do handoff original é histórica.
- Os controles IPv6 Google/Cloudflare falharam em sondas anteriores no Windows/Linux; não foram repetidos às 12:03 UTC. Não apresentar esses controles antigos como nova medição simultânea.
- Não houve contraprova nova por operadora independente, inspeção administrativa do túnel, teste de persistência após reboot ou confirmação do IP WAN nesta entrega.

## 4. O que o servidor já retornou e não deve ser repetido às cegas

O [retorno Linux do incidente](https://github.com/luziellacerda/Servidor-pix/blob/d4918a3d0735603dce9205fa580f29faa8e4a4b5/HANDOFF-INCIDENTE-CONECTIVIDADE-POS-TROCA-PLACA-SERVIDOR-20260908.md#12-retorno-%C3%BAnico-do-executor--preencher-aqui) informou túnel conectado, serviços locais ativos e sondas IPv4 funcionais. Falhas IPv6 também atingiram destinos de controle. O salto exato ainda não foi determinado; não houve reparo efetivo naquele retorno.

Há sessões reais Suite via IPv6 com HTTP 200 até **04:17:10 UTC / 01:17:10 locais**, depois do boot após a manutenção. Isso enfraquece atribuir a falha exclusivamente ao novo IP/nome da placa de rede; não prova quando ou por que o acesso parou.

Foi tentado `settings.ipv4_only=true` apenas no registro de `app`. Cloudflare recusou HTTP 400, código **9227**, recurso indisponível naquela zona. A releitura manteve o flag false e os 40 registros iguais. **Não repetir com outro token presumindo simples erro de autenticação**. Não desligar IPv6 de toda a zona, trocar plano ou expor a origem como alternativa automática.

A `main` revisada antes desta publicação estava em `fc53c069b7f214b74bfa5b51c32b25886ee20d75`: código PIX legado e documentação, **não a fonte apropriada para substituir toda a produção Suite**. A branch de operação `codex/repair-suite-connectivity-20260908` foi consultada em `f1aaba82aea09b2bf48063d32f16375c0e1cc793` sem merge de código ou deploy.

Baseline de API confirmado no retorno anterior: release `es-reopen-efaf1d3-20260905`, SHA-256 da DLL `e10bcf191c7b1c4b030427713b848a8e89af51517483d319b5979cd9ea7b07ef`. Identificar novamente caminho real, hash e InvocationID antes de qualquer mudança; não assumir que PIDs antigos ou o HEAD do Git sejam o processo atual.

## 5. Sequência objetiva para o executor autorizado

### Passo 1 — confirmar a mudança relatada, sem modificá-la

Conferir privadamente no servidor/roteador a WAN atual e perguntar/verificar **onde** foi feita a atualização mencionada: rede do servidor, destino do ingress, DNS ou outra configuração. Registrar horário e campos alterados, sem tokens. O endereço público informado não esclarece sozinho esses campos.

Consultar o DNS/ingress efetivamente carregado e todos os conectores. Comparar com o baseline: CNAME proxied do túnel, destinos loopback, regras de cada hostname, ordem dos ingress e destino final. Se houver diferença, documentar antes/depois; não desfazer a alteração do proprietário sem identificar sua finalidade e a falha concreta. Confirmar separadamente origem local e domínio público.

### Passo 2 — observar uma tentativa real do programa

Combinar com o proprietário uma única tentativa de entrada usando **a identidade já existente**, registrando início/fim com fuso, caminho/versão/hash do EXE e a etapa da mensagem. Não gerar nova licença, redefinir vínculo, pedir chave privada/OTP no chat ou encerrar sessões de terceiros.

Correlacionar a mesma janela em cliente, Cloudflare quando acessível, Nginx e API. Distinguir challenge, criação/renovação de sessão, heartbeat e carregamento inicial do catálogo: a mensagem genérica também pode ocorrer no catálogo. Não confundir chamadas das sondas com a instância real.

Se necessário, repetir uma sonda .NET sem autenticação com as opções de transporte do cliente: mesmo domínio, `SocketsHttpHandler`, sem proxy/redirecionamento, ConnectTimeout 10 s, HTTP 20 s, revogação online e validações de TLS pertinentes. A comparação IPv4 deve ser temporária e restrita à sonda, sem patch no produto, override persistente ou relaxamento de certificado/pin. Sucesso dessa sonda também não substitui login.

### Passo 3 — atribuir a falha à camada comprovada

| Evidência atualizada | Próxima investigação |
| --- | --- |
| O EXE não completa TCP/TLS e não há HTTP correlacionável | Rede do PC até Cloudflare; comparar famílias, controles e redes independentes. Ausência de journal da API, isoladamente, não localiza a falha |
| Cloudflare recebe, mas a origem não recebe | WAF/Access, regra do hostname, conector e destino do túnel; verificar limitações do logging |
| Nginx recebe, API não recebe | Método, caminho, headers e upstream exato; preservar portal e demais locations |
| API recebe e retorna erro | Registrar estágio/status/correlação; investigar protocolo, assinatura ou armazenamento conforme erro real, não por suposição |
| Sessão abre, catálogo falha | Rotas de conteúdo, prontidão local, autoridade/grant/gateway; não reativar licença para corrigir catálogo |
| Testes funcionam fora da rede afetada, mas IPv6 e controles falham nela | Inspeção autorizada de WAN/RA/delegação de prefixo/rota/operadora dessa rede; não declarar defeito universal da origem |

Repetir controles e família no mesmo intervalo; para concluir sobre acesso externo, usar redes realmente independentes com IPv6 funcional quando essa família for testada. O fato de Linux e Windows terem apresentado a mesma saída IPv4 em registros anteriores não fornece duas operadoras independentes.

### Passo 4 — propor e aplicar somente o delta demonstrado

Antes de executar, apresentar componente/arquivo/campo exato, causa, consumidores afetados, backup protegido, validação, janela e reversão. Mudanças de rede exigem acesso local/fora de banda e meio de recuperação; não reiniciar host/roteador nem renovar WAN às cegas.

Se a causa estiver fora da origem, explicar o limite de uma correção apenas no servidor. Não inventar ajuste SQL/Nginx ou trocar IP no cliente para resolver timeout anterior ao HTTP. Uma melhoria geral de transporte no produto, se comprovadamente necessária, é decisão separada do proprietário e precisa atender todos os clientes; não está implementada nem autorizada por este documento.

## 6. Cliente compilado: identificar sem misturar as entregas

A compilação local de comportamentos de download já foi concluída, separadamente deste incidente:

- Base Suite 2.0.2, commit `44c936ace6e8645edbfe9b15aeb093da35408504`, com alterações locais; não apresentar o commit-base como se contivesse essas alterações.
- Build UTC do manifesto: `2026-09-08T11:37:49.0802133Z`; Windows x64 self-contained, unsigned, **somente teste, não distribuição**.
- SHA-256 do EXE, reconferido nesta preparação: `577E5DDC7CE83FB39347F020F2F6D3E023AE3F72DA43A12C7C49DB156AB3A190`.
- Pacote local: `J:\TURBORAMA SUITE COMPILAÇÃO\VALIDACAO-DOWNLOADS-20260908\pacote\Turborama-2.0.2-win-x64-44c936ace6e8-UNSIGNED-NOT-FOR-DISTRIBUTION`.
- Escopo: comportamento de publicação/extração dos downloads selecionados na raiz TURBORAMA. Jogos PS4/PS5/Xbox One/Series permanecem no fluxo anterior. Licenciamento, domínio, autoridades e transporte de conexão não foram ajustados para o incidente.
- O hash `9f6d4dbf...` do incidente original identifica outra compilação anterior; não é o hash deste pacote. Primeiro identificar qual EXE está realmente aberto. Não substituir instalação ou iniciar outra instância automaticamente.
- Testes locais/offline passaram; login e download real autenticado ainda precisam de aceite. SmartScreen/assinatura é assunto separado da conectividade; não desligar a proteção para investigar rede.

## 7. Preservação e aceite de vários consumidores

Não alterar/resetar banco Suite ou estado PIX, limpar sessões gerais, recriar licença, regenerar chaves, trocar certificados/pins, ampliar privilégios, mudar contratos `/v1`, liberar portas internas ou fixar IP público/Cloudflare nos clientes. Preservar também TurboBox, outros sites e processos inventariados no manual operacional; não restringir o impacto ao produto Suite.

Preservar a reabertura ES e o cabeçalho exato `X-TurboRama-Client: EMULATIONSTATION`, sem aplicá-lo à Suite normal. Manter isolamento entre aplicações e clientes, autoridade/assinaturas, disponibilidade por item e Range/retomada. Não executar pagamentos, mensagens WhatsApp, revogações ou carga em produção como teste genérico.

| Frente | Aceite mínimo desta retomada |
| --- | --- |
| Rede/túnel | Domínio e ingress corretos, comparação IPv4/IPv6 com controles; explicar rede/camada restante e limitações |
| Suite existente | Identidade preservada; challenge, sessão, heartbeat e catálogo assinados aprovados no EXE identificado |
| ES | Abrir/sair/reabrir, coexistência com Suite e isolamento de sessões |
| PIX/portal/painéis | Fluxos autorizados de teste; autenticação/Access/CSRF preservados; nenhum pagamento real |
| Conteúdo | Arquivo de teste autorizado, integridade e retomada; validar extração/publicação separadamente do login |
| Outros consumidores | Verificações proporcionais ao componente compartilhado que for alterado, sem reiniciar serviços alheios |
| Persistência/reversão | Configuração efetiva e rollback documentados; reboot só em janela autorizada ou marcado pendente |

Nunca marcar como aprovado um teste não executado. Health 200, challenge GET 405, túnel conectado ou build aprovado não substituem o fluxo autenticado de ponta a ponta.

## 8. Retorno do executor desta retomada — preencher neste arquivo

| Item | Resultado / evidência sanitizada |
| --- | --- |
| Executor, host e janela UTC | Pendente |
| WAN atual e configuração que o proprietário alterou | `187.21.51.38` informado; verificação administrativa, campo e horário pendentes |
| DNS, ingress e conectores efetivos | Pendente nova leitura; manter baseline da seção 2 como histórico |
| EXE real, versão/hash, janela e etapa da falha | Pendente |
| Correlação cliente/Cloudflare/Nginx/API | Pendente; distinguir sondas de tentativa real |
| IPv4/IPv6, controles e redes independentes | Lote Windows da seção 3 disponível; contraprovas novas pendentes |
| Causa comprovada e camada | Pendente; não presumir IP público antigo, banco ou roteador como causa definitiva |
| Mudança executada, autorização e consumidores afetados | Nenhuma nesta entrega documental; preencher eventual delta autorizado |
| Backup, validação e rollback | Pendente se houver mudança |
| Matriz de aceite da seção 7 | Pendente; listar cada resultado, não apenas “tudo OK” |
| Arquivos/commits/releases resultantes e persistência | Publicação documental separada de eventual deploy; completar |
| Estado final e ação indispensável restante | Incidente não encerrado; login real ainda não comprovado nesta retomada |

Não incluir tokens Cloudflare, DSNs, headers Authorization/Cookie, OTPs, proofs, chaves, grants, URLs privadas, dumps de clientes ou configurações administrativas brutas. Evidências sensíveis ficam protegidas no servidor; o Git recebe somente resultados sanitizados.

## 9. Referências fixadas

- [Incidente e retorno Linux — d4918a3d](https://github.com/luziellacerda/Servidor-pix/blob/d4918a3d0735603dce9205fa580f29faa8e4a4b5/HANDOFF-INCIDENTE-CONECTIVIDADE-POS-TROCA-PLACA-SERVIDOR-20260908.md).
- [Manual completo de funções e operação — f1aaba82](https://github.com/luziellacerda/Servidor-pix/blob/f1aaba82aea09b2bf48063d32f16375c0e1cc793/HANDOFF-FUNCOES-E-OPERACAO-SERVIDOR-PIX-SUITE-20260908.md).
- [Integração ES e contratos compartilhados — eb522526](https://github.com/luziellacerda/Servidor-pix/blob/eb522526547be876982f3fabb79f59fefb8fb702/docs/suite/HANDOFF-EMULATIONSTATION-ROTAS-COMPARTILHADAS-20260905.md).
- [Cloudflare Tunnel: conexões de saída, sem necessidade de IP publicamente roteável](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/).
- [Cloudflare: `edge-ip-version` controla cloudflared → Cloudflare](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/configure-tunnels/run-parameters/#edge-ip-version).

O histórico orienta a investigação, não autoriza reaplicar implantações antigas. Este novo handoff não altera o funcionamento do servidor nem declara que o proprietário atualizou um campo específico sem verificação.
