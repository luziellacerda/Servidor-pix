# Handoff único — conectividade após troca da placa-mãe do servidor

Data: 08/09/2026. Fuso de referência: UTC, com horário local UTC−3 quando indicado.
Repositório: `luziellacerda/Servidor-pix`.
Estado desta entrega: diagnóstico externo e revisão de código/documentação; **nenhuma correção implantada no servidor**.

## 1. Pedido do proprietário e objetivo da execução

Após substituir a placa-mãe do servidor, desligá-lo e ligá-lo novamente, o proprietário informou que a TurboRama Suite passou a mostrar **“SERVIÇO DE LICENCIAMENTO TEMPORARIAMENTE INDISPONÍVEL”**. Há vários sistemas e muitos clientes consumindo a infraestrutura. A solução precisa tratar a causa e preservar tudo que já funciona; ajustar manualmente um PC não atende ao objetivo operacional.

**Atualização recebida durante a preparação deste handoff:** o proprietário informa que o responsável pelo servidor não encontrou nenhuma solicitação proveniente do programa. É relato operacional relevante, ainda sem extrato de log, janela exata e identificação de todos os destinos consultados. Essa informação reforça a investigação anterior à API e deve orientar a correlação da seção 5.3; não é motivo para alterar banco/licenças.

Este é o único documento de acompanhamento deste incidente. O executor com acesso ao servidor deve usar as evidências abaixo, identificar a causa efetiva, aplicar a correção mínima dentro do escopo autorizado e preencher o retorno da seção 12 **neste mesmo arquivo**. Não criar sucessivos arquivos HANDOFF/RETORNO/ORDEM para repassar o diagnóstico. Manter os documentos anteriores como histórico e referência de contratos; não reexecutar automaticamente instruções antigas de implantação.

A publicação deste arquivo não autoriza restauração destrutiva de banco, transferência de licenças, rotação de chaves, mudança geral de domínio ou substituição de outros produtos. As recomendações condicionais abaixo exigem evidência da camada defeituosa e um procedimento de reversão concreto. Nesta tarefa Windows, o pedido é produzir/publicar o handoff; acesso administrativo ao Linux, ao roteador e à conta Cloudflare não foi estabelecido.

## 2. Conclusões corrigidas e limites do diagnóstico

1. **Há falha reproduzível no caminho IPv6 desta rede Windows.** IPv4 alcança os endpoints; IPv6 também falha para domínios de controle independentes. A hipótese mais forte é problema de saída IPv6 no PC, roteador, operadora ou caminho até a borda. O salto exato ainda não foi identificado.
2. **A afirmação anterior “o Windows não tem rota IPv6” estava incorreta.** Na revisão, a interface ativa tem endereços IPv6 globais e rota padrão `::/0`. Existência de endereço/rota não comprova conectividade utilizável. Não repetir a conclusão incorreta.
3. **Não está provado que a troca da placa causou o problema IPv6**, nem que todos os clientes falham. É necessário correlacionar horários do boot/manutenção, rede do PC, rede do servidor e testes em outras operadoras. Se servidor e PC compartilham o roteador, investigar essa dependência; essa topologia não foi confirmada.
4. **A API Suite foi alcançada por IPv4 e uma consulta de licença fictícia retornou `LICENSE_NOT_FOUND`.** No código revisado, esse resultado vem depois da leitura PostgreSQL. É evidência de uma leitura funcional naquele instante; não comprova integridade de todos os dados, existência da licença real, gravação de sessões, funcionamento do catálogo ou de todos os consumidores. A frase anterior “a troca não apagou o banco” foi ampla demais e não deve ser usada como prova de preservação.
5. **O pin TLS conferiu nos dois IPv4 consultados.** Isso afasta divergência desse certificado naquele teste, mas não prova o estado de todas as bordas, máquinas, horários ou credenciais internas do servidor.
6. **HTML em `/health`, `/ready` e `/ready/content` públicos não comprova erro de proxy.** O domínio também hospeda um portal e o exemplo de Nginx mantém saúde/readiness locais. Não publicar nem remapear essas URLs apenas para fazer um monitor ficar verde.
7. **Timeout deste PC ao IP da borda Cloudflare, antes de completar TCP, não é corrigido por SQL, Nginx ou lógica de licenciamento na origem.** Se confirmado problema nesse trecho, a atuação central possível está na rede administrada/operadora ou na configuração elegível da borda. Uma mudança no Linux de origem não repara, por si só, a conectividade IPv6 de qualquer assinante da Internet.

## 3. Evidências coletadas no PC em 08/09/2026

### 3.1 Cliente e origem da mensagem

- Cliente analisado: TurboRama Suite `2.0.2`, commit `44c936ace6e8645edbfe9b15aeb093da35408504`.
- EXE de staging recompilado neste PC: SHA-256 `9f6d4dbf150b80abc566cfb6e5e24a8849e706c000e486da5d997e646c51e820`.
- Essa compilação é unsigned; a advertência SmartScreen é um assunto separado deste incidente de transporte.
- Uma das instâncias em execução foi identificada no pacote recompilado em `J:\TURBORAMA SUITE COMPILAÇÃO`. Havia duas instâncias abertas; a captura, sozinha, não identifica qual exibiu a mensagem. Não encerrar processos ou limpar sessões de terceiros para diagnosticar a rede.
- O cliente usa `https://app.lzgames.com.br/`, vindo do envelope público de autoridade incorporado; não abre conexão direta com PostgreSQL nem tem IP numérico de banco fixado.
- `SuiteLicenseClient` usa `SocketsHttpHandler`, `ConnectTimeout=10s`, timeout HTTP de `20s`, sem proxy, sem redirecionamento automático, revogação de certificado online e validação do SHA-256 da chave pública TLS.
- `PremiumLoginWindow.xaml.cs`, linhas 104–107, mostra a mensagem da captura para `HttpRequestException` ou `TaskCanceledException`. A mesma tela abrange sessão e leitura inicial do catálogo; a mensagem isolada não identifica qual requisição falhou. Negativas de licença/protocolo seguem outro tratamento.
- O probe .NET reproduziu timeout em aproximadamente `10034 ms`. Outro probe em memória, escolhendo endereços IPv4 via DNS, respondeu em `357 ms`, mantendo HTTPS e revogação online. São sondas de transporte, não um login completo. O ambiente PowerShell revisado é 7.6.5/.NET 10.0.11; o EXE também usa a família .NET 10. As sondas não registraram o erro interno da instância aberta.

Fontes do cliente: [handler e timeouts](https://github.com/luziellacerda/TRUBORAMA-SUITE/blob/44c936ace6e8645edbfe9b15aeb093da35408504/Licensing/SuiteLicenseClient.cs#L404), [tratamento da tela](https://github.com/luziellacerda/TRUBORAMA-SUITE/blob/44c936ace6e8645edbfe9b15aeb093da35408504/PremiumLoginWindow.xaml.cs#L55), [autoridades públicas](https://github.com/luziellacerda/TRUBORAMA-SUITE/tree/44c936ace6e8645edbfe9b15aeb093da35408504/authority/public).

### 3.2 DNS, transporte e respostas

Endereços DNS observados, não destinados a configuração fixa:

- A: `104.21.3.166` e `172.67.130.243`.
- AAAA: `2606:4700:3036::6815:3a6` e `2606:4700:3033::ac43:82f3`.
- O resolvedor usado pelo .NET entregou IPv6 antes de IPv4.
- Os IPs são da borda Cloudflare; não revelam o IP do Linux nem do banco.

| Momento UTC / teste | Resultado observado | Alcance da evidência |
| --- | --- | --- |
| 10:48, TCP direto aos dois A, portas 443/80 | Conexão em cerca de 49–58 ms | IPv4 até a borda alcançável |
| 10:48, `GET /` e saúde pública em `app` | HTTP 200, `text/html`, título “Portal do Consumidor · LZGames” | Portal atendido; não é saúde da API |
| 10:48:42, `GET /v1/suite/challenges` | HTTP 405, `Allow: POST`, sem body, `no-store`, correlação | Rota existe; GET não testa abertura de sessão |
| 10:48:42, `GET /v1/suite-content/catalog/current` | HTTP 405 com HTML Nginx | Compatível com bloqueio explícito de método no proxy; não prova catálogo quebrado |
| 10:48:42, `GET https://pix.lzgames.com.br/v1/health` | HTTP 200, JSON `ready:true`, `service:turborama-online` | PIX respondeu à sonda de saúde |
| 10:49:01, POST `{}` a challenge e activation/challenge | HTTP 400 `JSON_INVALID`, JSON sanitizado | Parser da API respondeu; não ativou licença |
| 10:50:45, challenge de identidade fictícia | HTTP 404 `LICENSE_NOT_FOUND` | Consulta de licença executada conforme implementação revisada |
| 10:57:55, HEAD challenge por IPv4 | HTTP 405, TLS ~0,171s, total ~0,278s | Nova confirmação externa a partir do PC |
| 10:57, mesmo destino por IPv6 | Timeout antes de TCP/TLS em ~6s | Falha IPv6 reproduzida nesta origem de teste |
| 10:57, controles `www.cloudflare.com` e `www.google.com` por IPv6 | Ambos timeout antes de TCP/TLS em ~6s | Falha não específica do servidor LZGames |

“Externa a partir do PC” significa tráfego público originado nesta rede Windows. **Não houve contraprova bem-sucedida de outra operadora nesta sessão.** A tentativa pela ferramenta de navegação externa foi recusada pela ferramenta antes da consulta; não é evidência de indisponibilidade do site.

As respostas HTTP desta tabela foram obtidas por sondas de diagnóstico, não pela instância do Turborama exibida na captura. Portanto, a API ter respondido a curl/sondas por IPv4 é compatível com o relato posterior de que **as tentativas do programa** não aparecem no servidor. Não usar os IDs das sondas para afirmar que o EXE chegou ou fez login.

Correlação para localizar no servidor, se os logs foram preservados:

| UTC | Requisição | X-Correlation-ID / CF-RAY |
| --- | --- | --- |
| 10:49:01 | Challenge com JSON vazio | `0d340c692a359706f4632d9d0c620152` / `a37d62582eae645a-GIG` |
| 10:49:01 | Activation challenge com JSON vazio | `21b9fd7cf51090744d39fe6babd0334f` / `a37d6259fa54de17-GIG` |
| 10:50:45 | Licença fictícia | `b6a9c694cedd878c590af921d2f4d900` / `a37d64e3efcf6d5e-GIG` |
| 10:57:55 | HEAD challenge IPv4 | `434a16c98791e363bf14a79b91ba5ac5` / `a37d6f625a322740-GIG` |

O identificador fictício usado foi `TS-DIAGNOSTIC-NOT-REAL`; nenhum ID real, OTP ou chave de cliente é necessário neste documento. Os testes podem produzir logs/contadores operacionais. A resposta de licença inexistente interrompe o fluxo antes da emissão/gravação de challenge, conforme o código revisado.

Certificado apresentado em ambos os IPv4: sujeito `CN=lzgames.com.br`, emissor `CN=WE1, O=Google Trust Services, C=US`. SHA-256 SPKI observado e esperado:

```text
13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7
```

### 3.3 O que falta provar

Login completo no mesmo EXE/identidade, resultado do catálogo e download, estado efetivo dos serviços após o boot, persistência dos dados reais, conectores Cloudflare ativos, logs do incidente e conectividade IPv6 por redes independentes permanecem pendentes. Não substituir essas provas por `HTTP 200`, `systemctl active`, CI aprovada ou uma consulta negativa ao banco.

## 4. Baseline e consumidores que precisam ser preservados

Na inspeção Git de 08/09, `main` estava em `a3bb0f20da79cc9ffe4779d2ab0526e53c952914` e contém o servidor PIX legado. A referência completa com Suite/ES revisada é `codex/emulationstation-suite-v1-20260905`, commit `eb522526547be876982f3fabb79f59fefb8fb702`. Publicar este handoff na `main` facilita encontrá-lo; **isso não transforma a main antiga no código correto para recompilar toda a produção**.

O último registro de implantação lido aponta API Suite em `efaf1d3cd3dfd2a807e9d5a0e7295328ff081c4a`, DLL `/opt/turborama-suite-r5-releases/es-reopen-efaf1d3-20260905/server/TurboRamaSuiteOnlineServer.dll`, SHA-256 `e10bcf191c7b1c4b030427713b848a8e89af51517483d319b5979cd9ea7b07ef`, schema 001–025. Esse é **baseline histórico de 05/09**, não confirmação do processo atual. Preservar alterações posteriores legítimas e identificar o runtime real por PID/InvocationID, argumento, arquivo resolvido, hash e manifesto. `WorkingDirectory`, nome do release ou HEAD do Git não bastam: drop-ins podem mudar `ExecStart`.

| Consumidor/componente | Contrato conhecido | Preservação obrigatória |
| --- | --- | --- |
| Portal do consumidor | `app.lzgames.com.br/`, HTML/assets e rotas próprias | Não substituir o virtual host inteiro nem o fallback do portal por API |
| PIX/gabinetes | `pix.lzgames.com.br/v1/*`; `turborama-pix.service`, loopback 5187 | Fluxos legados, contratos e estado protegido; não confundir com banco Suite |
| Painel PIX/BFF | `painelpix.lzgames.com.br/admin`, políticas Cloudflare Access | Autenticação, CSRF, permissões, host permitido, cookies; API máquina não pode exigir login humano |
| Suite licenciamento | `/v1/suite/activations/*`, `/v1/suite/challenges`, `/v1/suite/sessions`; API loopback 5190 | ProductId, DTOs, proofs, assinatura, contexto, identidade, sessões e limites |
| EmulationStation | Também POST em `/v1/suite/challenges` e `/v1/suite/sessions`, cabeçalho `X-TurboRama-Client: EMULATIONSTATION` | Repasse desse cabeçalho; Suite sem cabeçalho; isolamento das sessões/aplicações; reabertura ES já corrigida |
| EmulationStation legado | `/v1/suite/emulationstation/challenges` e `/v1/suite/emulationstation/sessions` | Manter compatibilidade dos clientes antigos ainda atendidos |
| Inventários de dispositivo/rede | Rotas e flags existentes na API Suite | Retenção, criptografia, mascaramento e vínculo; IP/MAC complementares não viram nova trava de licença |
| Catálogo/autorizações | POST `/v1/suite-content/catalog/current` e `/downloads/authorize` → API 5190 | Catálogo esperado de 902 itens no contrato revisado, assinaturas, disponibilidade e grants |
| Arquivos de conteúdo | GET `/v1/suite-content/artifacts/{grantId}` → gateway 5191 | Bearer, expiração, `Range`/`If-Range`, redirecionamento autorizado e origens privadas; não publicar URLs temporárias |
| Admin Suite | `turborama-suite-admin.service`, socket Unix existente | Não inventar porta pública; manter BFF, token interno, papel DB e auditoria |
| Publisher, monitor, janitor, alertas | Serviços/timers de conteúdo existentes | Credenciais separadas, não reprovisionar catálogo nem executar limpeza/republicação em massa |
| Presença/WhatsApp | Outbox e worker/timer existentes | Idempotência/cooldown; não enviar avisos reais durante diagnóstico nem duplicar eventos |
| Outros sites/processos | Inventário ainda a levantar no Linux/Cloudflare | Não presumir que esta tabela é todo o servidor; registrar todos antes de mudança compartilhada |

A Suite usa PostgreSQL, com `ConnectionStrings:SuiteStore` e conexões separadas para conteúdo/admin. O PIX legado usa `TURBORAMA_SERVER_STATE_FILE` (exemplo: `/var/lib/turborama-pix/state.json`) e chaves próprias. Não restaurar/migrar um armazenamento como se fosse o outro. Não abrir 5187/5190/5191/5432 à Internet.

## 5. Coleta no Linux após a troca da placa, antes de corrigir

Executar por console/sessão administrativa já autorizada. Registrar evidência bruta apenas em diretório protegido e compartilhar somente resumo sanitizado. Nunca copiar `Environment`, `/proc/.../environ`, tokens de `ExecStart`, DSNs completos, configuração Cloudflare bruta, headers de autorização, grants ou dumps de clientes para Git/chat. `nginx -T`, `systemctl cat` e journals podem conter dados sensíveis: inspecionar localmente com acesso restrito.

### 5.1 Hora, hardware de rede, rota e armazenamento

Comandos de leitura (resultados de IP/MAC e inventário ficam privados):

```bash
date -u --iso-8601=seconds
uptime -s
timedatectl status
ip -br link
ip -br address
ip -4 route show
ip -6 route show
ip -6 rule show
resolvectl status
findmnt --real
df -hT
df -i
systemctl --failed --no-pager
```

Se `resolvectl` não existir, identificar o gerenciador DNS realmente instalado; não instalar/substituir componentes para rodar o exemplo.

Verificar especificamente:

- Nome da NIC antes/depois (`enp...`/`eno...`), driver, carrier, erros/drop, bridge/bond/VLAN e perfis Netplan/NetworkManager/systemd-networkd. Troca de placa pode alterar nome e MAC, invalidando vínculo de perfil ou reserva DHCP. Não presumir qual gerenciador está em uso.
- Reserva DHCP, IP interno atual versus upstreams/firewall vinculados ao IP/MAC antigo; gateway, rotas IPv4/IPv6 e DNS. Se o túnel roda no mesmo Linux e usa loopback, mudança do IP LAN não exige mudar os endereços loopback.
- Roteador/operadora: prefix delegation, RA, gateway IPv6 alcançável, filtro ICMPv6/PMTU e rota de saída. Uma rota anunciada sem saída útil pode afetar vários PCs. Não bloquear ICMPv6 indiscriminadamente nem mudar MTU às cegas.
- Relógio após reset de BIOS/RTC, sincronização NTP e horário do primeiro boot. Desvio de hora afeta TLS, validade de envelopes, desafios e sessões.
- Montagem efetiva de volumes, UUIDs, espaço/inodes, permissões e diretórios de dados/chaves/credenciais. Volume ausente não deve ser substituído por pasta vazia nem disparar inicialização de um banco novo. Conferir se o serviço abriu o mesmo cluster/diretório histórico.
- Somente se houver erro demonstrado de credenciais seladas a TPM, investigar recuperação autorizada do material existente. Não regenerar chaves por causa da placa nova. A placa do servidor não é a identidade CNG/TPM dos PCs consumidores.

Antes de alterar a rede remotamente, manter console fora de banda/local e rollback testável. Não aplicar reset de rede/firewall nem reiniciar roteador/host como tentativa genérica.

### 5.2 Serviços, autostart, dependências e release efetivo

```bash
systemctl list-units --all --type=service 'turborama*' 'cloudflared*' 'nginx*' 'postgresql*' --no-pager
systemctl list-unit-files 'turborama*' 'cloudflared*' 'nginx*' 'postgresql*' --no-pager
systemctl list-timers --all --no-pager
ss -lnt
ss -lx
systemctl show turborama-suite-api.service turborama-pix.service \
  turborama-suite-admin.service turborama-suite-content-gateway.service \
  -p Id -p LoadState -p ActiveState -p SubState -p UnitFileState \
  -p FragmentPath -p DropInPaths -p MainPID -p InvocationID \
  -p ExecMainStartTimestamp -p NRestarts -p Result
```

Identificar todos os conectores, inclusive processos em contêiner e serviços com outro nome. Confirmar unidades de cluster PostgreSQL reais: a unit agregadora pode estar ativa sem provar o cluster certo. Inventariar os sites/tarefas não Turborama antes de qualquer reload/restart compartilhado.

Inspecionar privadamente os unit files, drop-ins, `EnvironmentFile`, `LoadCredential`, contas e diretórios. Confirmar disponibilidade de `/etc/credstore` quando usado e material montado em `CREDENTIALS_DIRECTORY`; conferir existência/owner/permissão sem imprimir conteúdo. Licenciamento pode iniciar enquanto conteúdo fica indisponível por credencial ausente.

Comparar boot atual/anterior, falhas de mount, restart loop e ordem de inicialização. Verificar dependências reais de rede/banco/mount; `After=network-online.target` sozinho não demonstra Internet pronta. Corrigir apenas dependência comprovadamente ausente; não inserir atrasos arbitrários nem um restart de todos os serviços.

### 5.3 Túnel, borda e Nginx

Levantar, por hostname/rota, o túnel e todos os conectores ativos, configuração local ou remota, destino e processo atendente. Comparar a configuração carregada após o boot com o backup anterior. Uma réplica antiga alcançável pode servir release/upstream diferente e produzir falha intermitente.

Com configuração local, validar ingress com os subcomandos de leitura da versão instalada (`cloudflared tunnel ingress validate` e `ingress rule` usando o caminho confirmado). Com túnel gerenciado remotamente, conferir a configuração efetiva no painel/API autorizada; validar um YAML inativo não prova nada. Não recriar túnel nem substituir credenciais.

No Nginx, conferir virtual host, ordem dos `location`, destinos por produto, headers `Host`/`X-Forwarded-*`/`X-TurboRama-Client`/correlação, confiança no proxy, cache e limites. Preservar os controles que impedem spoofing de IP. Não confiar em `CF-Connecting-IP` vindo de origem não autorizada.

Correlacionar falhas com logs de borda/túnel/Nginx/API usando horário UTC, rota e IDs da seção 3. Um SYN que nem alcança Cloudflare não aparece no journal da API. Um 405 gerado pelo Nginx de conteúdo também pode não chegar à API. A ausência de log, isoladamente, não indica queda do banco.

Para verificar o relato de que o programa não enviou solicitações ao servidor, combinar **uma tentativa coordenada no EXE existente**, sem OTP/nova ativação, com estes registros:

1. Confirmar caminho/hash da instância usada e anotar início/fim em UTC; esclarecer se a observação original cobria acesso HTTP, logs de erros ou apenas auditoria de licenciamento. Ausência em uma tabela de auditoria não equivale a ausência na rede.
2. No PC, observar destino/família de conexão e tempo até a mensagem, sem capturar segredos; identificar se houve tentativa IPv6, IPv4, TCP/TLS ou bloqueio local. Não confundir curl/browser com o processo do programa.
3. Na borda e em todos os conectores/upstreams efetivos, procurar a mesma janela; usar CF-RAY/correlação quando existirem. Se falhar antes de HTTP, esses IDs podem não ser gerados. Não exigir IDs inexistentes como condição para investigar TCP.
4. Se não houver conexão TCP completada nem requisição na borda, priorizar PC/rede/operadora até a Cloudflare. Se a borda receber e a origem não, inspecionar regra WAF/Access, túnel e destino. Se Nginx receber e API não, inspecionar `location`, método, headers e processo atendente.
5. Se a API receber, registrar status/estágio e encaminhar a investigação para protocolo/serviço/banco somente conforme o erro. Se houver logging amostrado/desabilitado ou só de falhas, registrar essa limitação; não concluir ausência de tráfego só por journal vazio.

O teste coordenado poderá renovar sessão legítima, conforme o comportamento normal do produto. Executá-lo com o usuário/conta já autorizado e preservar as demais sessões; não rodar um script que emita OTP, troque identidade ou dispare notificações sintéticas.

### 5.4 Saúde local e PostgreSQL

Somente após confirmar as portas/serviços ativos, executar:

```bash
curl --noproxy '*' --connect-timeout 3 --max-time 8 -sS -i http://127.0.0.1:5187/v1/health
curl --noproxy '*' --connect-timeout 3 --max-time 8 -sS -i http://127.0.0.1:5190/health
curl --noproxy '*' --connect-timeout 3 --max-time 8 -sS -i http://127.0.0.1:5190/ready
curl --noproxy '*' --connect-timeout 3 --max-time 8 -sS -i http://127.0.0.1:5190/ready/content
curl --noproxy '*' --connect-timeout 3 --max-time 8 -sS -i http://127.0.0.1:5191/health
curl --noproxy '*' --connect-timeout 3 --max-time 8 -sS -i http://127.0.0.1:5191/ready
```

Na versão revisada, `/health` da Suite indica vida do processo e `/ready` verifica a flag `Suite:Enabled`; **nenhum dos dois comprova banco**. `/ready/content` verifica configuração, compatibilidade de pepper com gateway e prontidão do catálogo; exigir corpo JSON e semântica correta.

No PIX, `ready:true` de `/v1/health` também não testa banco/pagamento externo: o caminho revisado usa a propriedade `IsReady` do gateway, que retorna `true` constante. Verificar estado e operação autorizada separadamente. Para o gateway de conteúdo, `/ready` local verifica prontidão da implantação/snapshot conforme o contrato do runtime; manter o resultado em ambiente protegido.

Pelo acesso PostgreSQL protegido já existente, confirmar banco/cluster e papel runtime reais antes de executar consultas. Não assumir `Database=postgres` só porque aparece nos exemplos. A consulta de migrations abaixo usa o papel administrativo já autorizado: acesso a `suite.schema_migrations` não é contrato garantido do papel runtime. Uma negativa nessa tabela não justifica novo GRANT. Exemplo de transação administrativa de diagnóstico no banco efetivo:

```sql
BEGIN READ ONLY;
SET LOCAL statement_timeout = '5s';
SET LOCAL lock_timeout = '1s';
SELECT current_database(), current_user, pg_is_in_recovery();
SELECT version FROM suite.schema_migrations ORDER BY version;
SELECT to_regclass('suite.suite_licenses'),
       to_regclass('suite.suite_devices'),
       to_regclass('suite.suite_sessions');
COMMIT;
```

Verificar grants mínimos, montagens e acesso como o papel da aplicação. Consulta feita como superusuário não valida permissões do serviço. Se houver `500`, localizar SQLSTATE e estágio com logs sanitizados. Não executar migrations antigas, `down`, reparação de cluster ou limpeza de sessões por suposição. A leitura negativa já observada não justifica qualquer alteração de banco.

## 6. Contraprova externa obrigatória por família e rede

Executar a mesma pequena matriz no servidor, na rede Windows afetada e em pelo menos duas redes externas independentes (uma móvel e outra fixa quando disponíveis). As redes usadas para afirmar sucesso IPv6 precisam comprovar IPv6 funcional em controles. Rede sem IPv6 útil não serve para reprovar o servidor por esse protocolo.

Usar uma ou poucas requisições por teste; não disparar benchmark ou loops de challenges em produção. Exemplos Bash de transporte público, sem credenciais:

```bash
curl --noproxy '*' -4 --connect-timeout 6 --max-time 12 -sS -I \
  -w '\nconnect=%{time_connect} tls=%{time_appconnect} total=%{time_total} code=%{http_code}\n' \
  https://app.lzgames.com.br/v1/suite/challenges
curl --noproxy '*' -6 --connect-timeout 6 --max-time 12 -sS -I \
  -w '\nconnect=%{time_connect} tls=%{time_appconnect} total=%{time_total} code=%{http_code}\n' \
  https://app.lzgames.com.br/v1/suite/challenges
curl --noproxy '*' -6 --connect-timeout 6 --max-time 12 -sS -I https://www.google.com/
curl --noproxy '*' -6 --connect-timeout 6 --max-time 12 -sS -I https://www.cloudflare.com/
```

No Windows usar `curl.exe` explicitamente. Registrar versão de curl/runtime, hora UTC, família, estágio de falha, status, tipo de conteúdo, `Allow`, correlação e CF-RAY quando houver. Nunca usar `-k` para transformar erro TLS em sucesso. Se fizer comparação por IP individual, usar `--resolve` temporário com endereço recém-resolvido e manter hostname/SNI; não editar `hosts`.

HEAD/GET 405 nas rotas POST é evidência de transporte/encaminhamento. Para saúde funcional, usar os testes autorizados da seção 10. Não repetir JSON vazio esperando HTTP 200; 400 é o resultado correto desse teste. Qualquer teste com identidade fictícia deve usar fixture reservada/inexistente e não tocar no vínculo real.

## 7. Decisão de correção central com base na evidência

| Resultado confirmado | Correção a considerar | Validação antes do aceite |
| --- | --- | --- |
| API local falha após boot, mount/credencial/dependência ausente | Restabelecer montagem/configuração existente e ordem de inicialização do componente afetado | Mesmo banco/chaves/release; saúde local e fluxo completo |
| Local funciona; somente um túnel/conector leva ao processo errado | Corrigir o destino/configuração divergente, preservando os demais ingress | Todos os conectores atendem contratos iguais; ausência de alternância de versão |
| Borda recebe; rota retorna portal/Access onde deveria retornar protocolo | Corrigir regra exata de hostname/path/header comprovadamente divergente | Portal e painel preservados; API máquina sem login humano; Suite/ES/content aprovados |
| IPv6 funciona nos controles de redes independentes, mas falha para `app` em várias redes | Investigar Cloudflare/peering/rota específica com timestamps/CF-RAY/traceroute | IPv4 e IPv6 aprovados fora da rede local, sem mudança de identidade |
| IPv6 falha também para controles apenas na rede afetada | Corrigir roteador/RA/prefix delegation/operadora dessa rede, se administrada; encaminhar evidências ao responsável se externa | Controles e `app` passam na mesma rede; demais clientes mantidos |
| Várias redes têm transporte OK, mas protocolo real dá erro | Investigar status/correlação/assinatura/SQLSTATE e estágio exato | Challenge, sessão, heartbeat e catálogo reais aprovados |
| Tudo passa exceto o EXE de uma origem | Reproduzir erro da mesma versão/runtime e rede; não alterar banco por esse indício | Prova da causa restante e decisão única de produto, sem afirmar solução global fictícia |

### 7.1 IPv6 da borda e origem são ligações diferentes

O percurso é **cliente → Cloudflare → túnel/origem → serviço → banco**. Os AAAA públicos podem ser gerados pela Cloudflare, independentemente do endereço usado para alcançar a origem. Remover um AAAA de origem ou mudar `cloudflared --edge-ip-version` não demonstra que o cliente passou a usar IPv4. `Pseudo IPv4` trata cabeçalhos de IP e não resolve timeout TCP anterior ao TLS. [Referência oficial de compatibilidade IPv6](https://developers.cloudflare.com/network/ipv6-compatibility/).

### 7.2 Mitigação central de IPv4, somente se necessária e viável

Se a investigação justificar uma mitigação na borda para o hostname e a conta suportar isso, avaliar a opção documentada por registro `settings.ipv4_only`. A API a descreve para situações excepcionais, restringindo respostas geradas a A sem alterar o transporte até a origem. **Disponibilidade/permissões, tipo de registro/túnel e efeito real não foram verificados nesta conta**; não prometer nem aplicar como solução já aprovada. [Contrato oficial do registro DNS](https://developers.cloudflare.com/api/resources/dns/subresources/records/methods/edit/).

Antes de qualquer execução: identificar registro exato, dependentes do mesmo hostname (incluindo portal e ES), configuração anterior, comportamento dos resolvedores/TTL, impacto em redes IPv6-only/NAT64 e rollback. Manter domínio, proxy, HTTPS e chave TLS compatíveis. A mitigação deve ter escopo e prazo, além de testes de todas as famílias/redes afetadas. Não é conserto de uma rota IPv6 defeituosa nem garantia de acesso por qualquer provedor.

A opção geral IPv6 Compatibility é de zona e sua customização é documentada para Enterprise. Desligá-la globalmente pode afetar todos os hostnames proxied; não é o procedimento padrão deste incidente. Se a única alternativa exige mudança de zona, exposição da origem, hostname ou certificado, apresentar o delta e os impactos concretos ao proprietário; não executar por associação com o pedido de handoff. [Limites oficiais da configuração](https://developers.cloudflare.com/network/ipv6-compatibility/).

Não remover a Cloudflare, desativar TLS/pinning, bloquear IPv6 no WAF para “forçar fallback”, fixar os IPs anycast ou editar cada PC. Bloqueio HTTP não garante nova tentativa IPv4 do cliente.

### 7.3 Limite real de uma solução apenas no servidor

A prioridade solicitada é resolver centralmente e conservar os EXEs existentes. Se as contraprovas demonstrarem que a origem e a borda funcionam e a falha está em redes não administradas, documentar isso no retorno, com evidência e alternativa concreta. Nenhuma mudança SQL resolve esse caso. Uma eventual melhoria geral de fallback no produto seria uma entrega separada para todos os clientes, se escolhida pelo proprietário, não uma exigência de configurar individualmente PCs nem algo implantado por este handoff.

## 8. Mudança mínima, persistência e reversão

Antes de mudar o componente comprovadamente defeituoso, registrar um baseline e backup protegido recuperável da configuração relevante, artefatos/releases atuais e metadados do banco; se a intervenção tocar dados, usar o procedimento de backup consistente já adotado, com restauração validada em ambiente separado. Nenhum backup, DSN ou chave entra no Git.

O plano executável precisa identificar: causa, arquivos/registro/unidade exatos, diff, dependentes, efeito esperado, verificação, gatilho de rollback e estado anterior. Não substituir arquivos completos de rede/Nginx/túnel quando um delta pequeno resolve. Validar a sintaxe antes de aplicar. Preferir reload do Nginx após `nginx -t` se isso for suficiente; restart só do serviço necessário. Qualquer alteração da API 5190 também atinge ES/conteúdo: considerar essa dependência na janela.

Não recompilar runtime para uma falha apenas de configuração/rede. Se houver bug de código demonstrado, construir em pasta nova a partir da fonte compatível com o runtime atual, testar a regressão e preservar o release anterior. Não copiar apenas DLL sobre processo em uso. `systemctl daemon-reload` só se houver mudança de unit/drop-in; isso não recarrega por si só a configuração de toda aplicação.

Rollback de configuração/artefato deve restaurar apenas o delta desta intervenção. Não executar scripts históricos que revertam PIX, admin e API juntos. Não restaurar automaticamente um snapshot do banco por regressão HTTP, pois isso pode apagar operações novas de outros clientes. Não reverter migrations em massa. Para mudança de rede, console e reversão precisam estar disponíveis antes de arriscar perder a sessão remota.

Depois da correção, verificar persistência das configurações e comportamento de inicialização. Um reboot controlado para homologação afeta todos os consumidores e precisa de janela operacional apropriada; não o executar agora por rotina. Se não houver janela, registrar “persistência configurada; reboot de aceite pendente”, sem declarar essa etapa concluída.

## 9. Preservações obrigatórias e observabilidade

Preservar licenças, estado PIX, banco Suite, dados comerciais, sessões de terceiros, inventários/histórico, OTPs, autoridades/envelopes, chaves de assinatura online, peppers, chave AES, certificados, pins, secrets systemd, permissões de filesystem/DB e contratos `/v1`. A chave do emissor offline e a chave de assinatura online têm papéis diferentes: não inventariar/publicar conteúdo de nenhuma delas. Reparo de conectividade não justifica rotação.

Preservar a correção de reabertura ES `efaf1d3`, isolamento Suite/ES/cliente A/B, filtros estritos de cabeçalhos e máscaras do painel. Não usar whitelist do IP do PC como substituto de prova criptográfica. Não aumentar globalmente permissões/limites por causa de um timeout pre-TLS.

Muitos clientes podem compartilhar um IP por NAT/CGNAT. Ao revisar limites de borda/proxy, medir 429 por rota e identidade conforme arquitetura existente; não removê-los nem supor que um IP equivale a um cliente. Separar o problema de transporte da capacidade do serviço. Os testes históricos de carga em CI não demonstram SLA/capacidade atual após a troca da placa.

Preservar a política de pools já implementada: no código revisado, Suite e admin têm máximo padrão de 8 conexões, respeitando limite explicitamente configurado. Não elevar todos os pools para 100 nem aumentar `max_connections` como tentativa de resolver reconexões. Medir uso total, espera e concorrência dos demais sistemas antes de qualquer ajuste de capacidade.

Monitoramento deve distinguir: TCP/TLS IPv4 e IPv6 em redes com suporte, protocolo público, vida do processo, banco, conteúdo/gateway e erro funcional autenticado. Usar métricas/status sanitizados. Se não houver monitor central, deixar uma implementação concreta no sistema de monitoração já adotado para decisão do operador; não criar um serviço ou alertas externos sem destino definido. Nunca usar apenas o HTML 200 do portal como saúde Suite.

Registrar exceção/estágio, rota, status, duração, correlação, PID/InvocationID e SQLSTATE quando pertinente. Não registrar request body, proof, assinatura, OTP, grant, DSN, chave ou URLs privadas. Não imprimir `Location` completo de um download autenticado no relatório.

## 10. Matriz de aceite — preservar múltiplos sistemas

Usar contas/fixtures de teste já autorizadas, sem cobranças ou WhatsApp reais. Testes de escrita/concorrência e carga executam primeiro em staging com contratos iguais aos atuais; smoke de produção deve ser pequeno e coordenado. Não cadastrar/revogar usuários reais para satisfazer a matriz. Não marcar teste indisponível como aprovado.

| Frente | Prova exigida |
| --- | --- |
| Rede | IPv4 em todas as redes de teste; IPv6 em redes que comprovem suporte; comparação afetada/externas; TCP, TLS, pin, status e latência |
| Suite 2.0.2 existente | Sem nova ativação: challenge assinado + sessão autorizada + heartbeat + catálogo; validar assinatura/contexto do cliente, não só HTTP |
| Licenças/isolamento | Licença/dispositivo inválido continua negado; cliente B não perde sessão/dados pelo reparo ou teste do cliente A |
| EmulationStation | Contrato compartilhado com cabeçalho exato; abrir/sair/reabrir com identidade existente; Suite e outra identidade continuam isoladas |
| PIX/gabinete | Saúde e autenticação/fluxo de teste autorizado; estado e comportamento de gabinete preservados; zero cobranças reais |
| Portal e painéis | Portal/assets carregam; painéis mantêm Access/autenticação/CSRF/permissões; API não redireciona para login HTML |
| Conteúdo | Readiness local verdadeiro, catálogo conforme contrato (902 no baseline), grant autorizado, arquivo de teste íntegro, Range/retomada e extração; expiração/negação continuam válidas |
| Presença/outbox | Sessão atualiza conforme regras existentes; nenhuma duplicação nem notificação real disparada pelo teste |
| Serviços/dependências | Sem restart loop; mounts/credenciais e cluster corretos; unidades não afetadas conservadas; todos os conectores usam destinos compatíveis |
| Persistência | Configuração efetiva mantida após reinício controlado aprovado ou teste pendente explicitamente registrado |
| Regressão/capacidade | Resultados antes/depois nas mesmas condições; concorrência A/B e aplicações em staging; observar latência/erros em produção, sem benchmark invasivo |

Se a mitigação opcional `ipv4_only` for deliberadamente adotada, a matriz deve registrar a ausência intencional de AAAA e validar o contrato DNS resultante e o acesso de clientes IPv6-only/NAT64. `curl -6` sem endereço nessa condição não significa pane da origem, mas também não pode ser marcado como IPv6 aprovado: registrar “IPv6 direto indisponível por mitigação”, seus impactos e a pendência de restauração.

Não é aceite: apenas trocar a mensagem da tela, mandar o usuário desativar IPv6, voltar à main antiga, resetar o banco, reativar a licença, encerrar sessões gerais, obter somente health 200 ou declarar que todos os clientes estão resolvidos a partir deste único PC.

## 11. Referências e tratamento dos documentos antigos

As referências Git abaixo estão fixadas no commit revisado; não dependem de links relativos para arquivos que não existem na main antiga. Conferir o runtime instalado e novas alterações antes de usar qualquer procedimento histórico.

- [Handoff da integração ES e reabertura](https://github.com/luziellacerda/Servidor-pix/blob/eb522526547be876982f3fabb79f59fefb8fb702/docs/suite/HANDOFF-EMULATIONSTATION-ROTAS-COMPARTILHADAS-20260905.md).
- [Evidência histórica de implantação efaf1d3](https://github.com/luziellacerda/Servidor-pix/blob/eb522526547be876982f3fabb79f59fefb8fb702/docs/suite/evidence/es-reopening-deployment-20260905.json).
- [Exemplo Nginx de conteúdo: métodos/ports e saúde local](https://github.com/luziellacerda/Servidor-pix/blob/eb522526547be876982f3fabb79f59fefb8fb702/ops/production/nginx-turborama-suite-content.locations.conf.example).
- [Program da API: saúde/readiness e inicialização](https://github.com/luziellacerda/Servidor-pix/blob/eb522526547be876982f3fabb79f59fefb8fb702/src/TurboRamaSuiteOnlineServer/Program.cs#L140).
- [Challenge e LICENSE_NOT_FOUND](https://github.com/luziellacerda/Servidor-pix/blob/eb522526547be876982f3fabb79f59fefb8fb702/src/TurboRamaSuiteOnlineServer/SuiteService.cs#L48) e [leitura PostgreSQL](https://github.com/luziellacerda/Servidor-pix/blob/eb522526547be876982f3fabb79f59fefb8fb702/src/TurboRamaSuiteOnlineServer/Store.cs#L55).
- [Dispatcher de escopo ES](https://github.com/luziellacerda/Servidor-pix/blob/eb522526547be876982f3fabb79f59fefb8fb702/src/TurboRamaSuiteOnlineServer/SharedEmulationStation.cs).
- [Tutorial completo do servidor](https://github.com/luziellacerda/Servidor-pix/blob/eb522526547be876982f3fabb79f59fefb8fb702/HANDOFF-TUTORIAL-COMPLETO-SERVIDOR-TURBORAMA-SUITE-20260903.md) e [ordem histórica de reparo de sessão](https://github.com/luziellacerda/Servidor-pix/blob/eb522526547be876982f3fabb79f59fefb8fb702/ORDEM-UNICA-EXECUCAO-REPARO-SESSAO-R25-SEM-NOVOS-HANDOFFS-20260903.md): úteis para contratos/evidências, não autorização para reaplicar migrations antigas.
- [Disponibilidade/réplicas cloudflared](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/configure-tunnels/tunnel-availability/): inventariar réplicas e destinos; criar outra réplica sem verificar o upstream não comprova redundância funcional.

Há documentos iniciais no repositório descrevendo fases anteriores ou candidato ainda não implantado. Resolver divergência usando commit, data, evidência e processo real. Não escolher arbitrariamente o documento mais permissivo nem desfazer correções posteriores.

## 12. Retorno único do executor — preencher aqui

Esta seção é o registro vivo deste incidente. Atualizar no mesmo caminho, por commits; não abrir outro handoff. Preservar a evidência histórica acima e distinguir medições novas.

| Item | Retorno do executor Linux em 08/09/2026 |
| --- | --- |
| Executor, host e janela UTC | Codex no servidor Linux de produção autorizado pelo proprietário. Diagnóstico local desde 09:46 UTC; execução deste handoff iniciada às 11:10 UTC. Evidências novas desta etapa até aproximadamente 11:21 UTC. |
| Momento da troca/boot e topologia PC/servidor | Troca da placa do servidor confirmada pelo proprietário. NIC/IP LAN mudaram; Nginx, PIX e Suite API iniciaram em 07/09 às 20:14:41 UTC. As sondas Windows identificadas na seção 3 saíram pelo mesmo IPv4 público observado no conector Linux; isso sugere saída de Internet compartilhada, sem provar sozinho a topologia física. Há sessões Suite via IPv6 com HTTP 200 até 08/09 às 04:17:10 UTC, horas após o boot. |
| Causa-raiz e camada | Falha de conectividade IPv6 reproduzida no Linux e nas sondas Windows, anterior ao HTTP da API. No Linux, TCP/443 por IPv4 conecta a app/Google/Cloudflare em 47–53 ms; IPv6 falha por timeout de 5 s nos três destinos. Tracepath IPv6 alcança o roteador e dois saltos da operadora; o salto exato da falha ainda não foi determinado. Não foi demonstrado defeito no Nginx, banco ou material de licença como causa deste timeout. |
| Relato de ausência de solicitações do EXE | Conferidos access.log Nginx e journals PIX/Suite. Até 10:37:17 UTC não houve novo login do EXE; os três POSTs recentes eram sondas locais. As sondas Windows das 10:48:42, 10:49:01, 10:50:45 e 10:57:55 UTC aparecem com rotas/status correspondentes à seção 3. A última sessão HTTP 200 continuava em 04:17:10 UTC. Esses testes não equivalem a login pelo executável. Logs da borda Cloudflare não foram disponibilizados pela credencial atual; não se declara captura de todo TCP na borda. |
| Contraprovas por operadora/família | Linux e Windows corroboram a falha IPv6 e acesso IPv4; ambas as origens observadas compartilham o IPv4 público. Não há contraprova concluída por duas operadoras independentes nem teste IPv6-only/NAT64. |
| Baseline real | API PID 6192, InvocationID 07f9f1884c0846f99a9d3321bc7dba7d, DLL efetiva do release es-reopen-efaf1d3-20260905, SHA-256 e10bcf191c7b1c4b030427713b848a8e89af51517483d319b5979cd9ea7b07ef, igual ao baseline histórico. PIX PID 6187; admin PID 3071; gateway PID 6195. Todos ativos, sem reinícios após seu início no boot. PostgreSQL de produção/schema suite identificado anteriormente por configuração e consultado em modo somente leitura; duas licenças e dois dispositivos ativos, sem alterações desde 07/09 nas colunas auditadas. |
| Túnel e DNS efetivos | API Cloudflare confirmou um conector ativo do túnel lz-fix, versão cloudflared 2026.8.3, quatro conexões, configuração remota versão 15. app → loopback:80; pix/painelpix → loopback:5187. app é CNAME proxied para esse túnel. Os outros túneis listados estavam sem conexões. Credencial atual tem DNS Read/Write e Zone Read; leitura de settings/ipv6 da zona retornou 403/9109. |
| Mudança aplicada | Nenhuma alteração efetiva em produção. Foi tentado exclusivamente PATCH settings.ipv4_only=true no registro app, conforme seção 7.2, com backup e rollback preparados. Cloudflare recusou HTTP 400, código 9227: recurso IPv4 Only/IPv6 Only indisponível para esta zona (plano Free observado). Nova leitura confirmou ipv4_only efetivo false e os 40 registros DNS integralmente iguais ao baseline, incluindo modified_on. Não repetir com outro token supondo que a limitação seja apenas de autenticação. |
| Backup/rollback | Snapshot DNS e configuração efetiva do túnel, hashes/runtime e sondas salvos em diretório local protegido evidence/reparo-suite-20260908 (0700; JSONs 0600). Nenhuma credencial foi impressa ou incluída no Git. Script local de mitigação valida hostname/túnel/campos antes do PATCH e possui reversão limitada ao mesmo flag. Não foi necessário executar rollback porque a escrita foi rejeitada e a releitura comprovou ausência de mudança. Nenhum backup de banco novo foi necessário para esta tentativa DNS; nenhum dado foi escrito. |
| Suite/ES/PIX/portal/admin/conteúdo/outbox | PIX local/público HTTP 200 ready:true; Suite /ready local HTTP 200; /ready/content e gateway /ready HTTP 200 status:ready; portal HTTP 200 HTML; GET público de challenge HTTP 405 esperado; painel público continua HTTP 302 para Access. Readiness não comprova login completo. Suite existente, ES reabrindo, heartbeat, catálogo assinado, download/Range e isolamento de sessões continuam pendentes de teste autenticado legítimo. Não foram gerados pagamentos, OTPs, grants ou mensagens WhatsApp. |
| Preservação de outros consumidores | Nenhum reload/restart, alteração de binário, unit, Nginx, firewall, banco, segredo ou DNS foi efetivado. Os 40 registros DNS e destinos existentes foram preservados. O inventário local de portas anterior foi mantido como evidência privada; problemas alheios ao login não foram alterados nesta intervenção. |
| Persistência após boot | Nenhuma configuração de produção nova para homologar. Unidades observadas estavam ativas após o boot existente. Reboot de aceite não executado; não há alegação de teste completo após novo reboot. |
| Commit/configuração/release resultantes | Atualização documental no mesmo handoff em branch codex/repair-suite-connectivity-20260908. Releases de produção permanecem os mesmos; main histórica não foi implantada. |
| Estado final | Reparo iniciado, sem correção efetiva ainda. Mitigação por hostname indisponível nesta zona. Acesso administrativo ao roteador solicitado ao proprietário, sem solicitação de senha no chat; painel privado acessível por HTTPS, sem sessão administrativa disponível ao executor. Inspeção de WAN/IPv6 e eventual correção dependem desse acesso ou de atuação da operadora. Aceite no EXE existente permanece obrigatório. |

### 12.1 Resultado executável e próxima intervenção

1. **O que foi tentado:** alteração isolada de settings.ipv4_only em app, mantendo CNAME, túnel, proxy, TTL e certificado. A finalidade era impedir que o EXE atual escolhesse o caminho IPv6 que não completa TCP nesta rede. A API recusou por indisponibilidade do recurso, antes de qualquer mudança persistida.
2. **Por que a investigação continua na rede:** além das sondas Windows, o Linux falha em IPv6 para dois controles independentes. Os registros históricos mostram que sessões via IPv6 funcionaram até 04:17:10 UTC, depois da manutenção. Isso enfraquece a hipótese de que apenas o novo nome/IP da NIC tenha quebrado o acesso público; não determina o instante exato em que a rede deixou de funcionar.
3. **Ação mínima pendente:** obter sessão administrativa autorizada no roteador e inspecionar estado WAN IPv6, delegação de prefixo, validade/RA, rota e diagnósticos originados no próprio roteador. Foi aberta no desktop do servidor uma janela Chrome separada, com perfil privado e controle restrito a loopback, na tela de login do painel Claro. Nenhuma senha foi solicitada no chat nem tentativa de adivinhação realizada. Comparar com o caminho LAN antes de escolher qualquer alteração. Não reiniciar o roteador, renovar WAN, trocar prefixo, alterar firewall ou desligar IPv6 às cegas.
4. **Limites de uma mudança na origem:** forçar IPv4 no processo Linux cloudflared, abrir 5190 ou editar SQL não corrige o TCP do Windows até a borda Cloudflare. Desligar IPv6 globalmente na zona ou expor a origem não foi adotado como alternativa à rejeição do recurso por registro.
5. **Aceite pendente:** depois de corrigida a camada comprovada, correlacionar uma tentativa do EXE existente com challenge/sessão/heartbeat e catálogo, preservando licenças e demais consumidores. Até lá, não marcar o incidente como resolvido nem a matriz da seção 10 como aprovada.

O retorno final precisa explicar **o que falhou, por que a correção resolve a camada comprovada, quais sistemas foram testados e o que ainda falta**, com evidências sanitizadas. Se não houver acesso/feature/janela indispensável, registrar o bloqueio concreto e a ação mínima necessária nesta seção. Não declarar “resolvido globalmente” enquanto o teste do cliente existente e a matriz compartilhada permanecerem pendentes.
