# Handoff consolidado — indisponibilidade, diagnóstico e recuperação do acesso à Turborama Suite

Data: **08/09/2026**. Horário local deste documento: America/Fortaleza, UTC−3.
Pedido: documentar o problema e o atendimento do início ao fim, incluindo como diagnosticar e reparar uma recorrência sem destruir o ecossistema existente.
Estado: **acesso ao programa confirmado pelo proprietário; causa-raiz da intermitência não determinada; downloads e aceite completo dos demais consumidores ainda pendentes**.

## 1. Resultado e leitura correta deste handoff

Após a troca da placa-mãe e o desligamento/religamento do servidor, foi relatada a mensagem **“SERVIÇO DE LICENCIAMENTO TEMPORARIAMENTE INDISPONÍVEL”**. O diagnóstico encontrou falhas de conexão IPv6 antes do HTTP, enquanto IPv4 alcançava o domínio. Posteriormente, IPv6 voltou a responder tanto no Linux quanto neste Windows, mantendo o túnel e a produção existentes. Foi aberta a nova compilação de teste, e o proprietário respondeu **“agora foi”**, confirmando a entrada na sequência do teste de login.

**Não houve um comando ou patch comprovadamente responsável pela recuperação da rede.** Não é correto dizer que recompilar, atualizar o A de `db`, reiniciar o túnel ou mudar o banco resolveu o problema. A alteração de produção tentada pelo executor foi recusada pela Cloudflare; as demais intervenções registradas foram diagnósticas/documentais. O proprietário relatou uma atualização de IP, mas não existe nesta entrega evidência causal ligando essa edição à recuperação do IPv6.

O resultado final tem três níveis distintos:

| Nível | Estado comprovado |
| --- | --- |
| Transporte | IPv4 e IPv6 responderam no Linux às 09:52–09:53 e neste Windows às 10:00, com os limites descritos abaixo |
| Entrada no programa | Confirmada pelo proprietário após abertura do pacote de teste identificado na seção 7 |
| Homologação integral | Ainda não concluída: correlação autenticada no servidor, heartbeat prolongado, catálogo/download real, retomada, ES e demais consumidores |

Este é o novo documento de referência para a continuidade, criado por pedido explícito posterior do proprietário. Os handoffs anteriores permanecem como histórico. Para evitar instruções concorrentes, registrar novas medições/retorno na seção 12 deste arquivo, salvo novo pedido explícito. Publicar este documento não executa reparos, não implanta código e não autoriza automaticamente mudanças de produção ou nos computadores dos clientes.

## 2. Sintoma, contexto e hipóteses que foram descartadas ou limitadas

- Há vários produtos e muitos clientes compartilhando o servidor. Uma solução que dependa de editar manualmente cada PC não atende ao objetivo.
- O proprietário informou inicialmente que nenhuma solicitação do programa estava chegando ao servidor. Sondas posteriores chegaram, mas **sonda não equivale a tentativa do EXE**.
- A mensagem da tela não significa necessariamente licença inválida ou banco perdido. No código revisado, ela trata `HttpRequestException` ou `TaskCanceledException` durante abertura de sessão **ou leitura inicial do catálogo autorizado**.
- “AUTORIDADE DO TURBORAMA SUITE INDISPONÍVEL” é outro tratamento: exige conferir o material público incorporado e sua validação. “LICENÇA, CÓDIGO OU DISPOSITIVO NÃO AUTORIZADO” também segue outro caminho. Não tratar todos esses estados como o mesmo erro de rede.
- A tela azul do SmartScreen mostrada anteriormente é separada: trata da execução/confiança no arquivo Windows. Não explica, sozinha, o timeout de uma aplicação que já iniciou. Não desabilitar proteção do Windows para diagnosticar rede.
- A troca da placa pode afetar nome/MAC da interface, reservas DHCP, rotas, relógio, montagens ou disponibilidade de credenciais. Isso exige verificação, não recriação automática do banco, do túnel ou das licenças.
- Os registros mostraram sessões IPv6 bem-sucedidas **depois** do boot da manutenção. Não foi demonstrado que a troca física, por si só, causou a falha posterior.

Fontes do cliente: [handler TLS/HTTP](https://github.com/luziellacerda/TRUBORAMA-SUITE/blob/44c936ace6e8645edbfe9b15aeb093da35408504/Licensing/SuiteLicenseClient.cs#L404) e [fluxo da tela de login](https://github.com/luziellacerda/TRUBORAMA-SUITE/blob/44c936ace6e8645edbfe9b15aeb093da35408504/PremiumLoginWindow.xaml.cs#L55). Esses trechos foram reconferidos na fonte local; não receberam alterações nesta entrega.

## 3. Arquitetura real: domínio, túnel, serviços e armazenamento

Percurso de acesso: **cliente → `https://app.lzgames.com.br/` → Cloudflare → túnel `lz-fix` → Nginx → API Suite → PostgreSQL local**.

O `cloudflared` inicia conexões de saída com a Cloudflare. Esse modelo não exige IP público fixo e normalmente não exige atualizar o endereço público no cliente quando a WAN muda. O cliente continua usando o domínio. A ligação cliente–Cloudflare é separada da ligação cloudflared–Cloudflare; forçar a família IP de uma não força a da outra. [Cloudflare Tunnel](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/) e [escopo de `edge-ip-version`](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/configure-tunnels/run-parameters/#edge-ip-version).

Baseline reconferido pelo executor Linux no retorno de 09:58, não uma nova leitura administrativa desta entrega Windows:

| Componente | Configuração/estado observado | O que preservar |
| --- | --- | --- |
| IP público de saída do Linux | `187.21.51.38`, confirmado por consulta HTTPS e pelo conector | Não gravar esse IP no cliente como destino de licença |
| `db.lzgames.com.br` | A somente DNS para `187.21.51.38`; última modificação registrada 07:21:53 locais | Não confundir com o destino da Suite; não alterar outros consumidores desse registro |
| `app.lzgames.com.br` | CNAME proxied para o túnel; ingress `http://127.0.0.1:80` | Portal e roteamento por caminhos no Nginx |
| `pix` e `painelpix` | CNAMEs proxied para o mesmo túnel; ingress local 5187 | PIX, painel/BFF, Access e autenticação |
| `lz-fix` | Um conector 2026.8.3, quatro conexões; configuração remota v15, dez hostnames | Todas as regras, ordem de ingress e fallback 404 |
| Suite e ES | API local 5190 | `/v1`, assinaturas, contexto, identidade, sessões e limites |
| Conteúdo | Autorizações na API; gateway local 5191 | Grants e redirecionamento autorizado; não publicar origens privadas |
| Admin Suite | Socket Unix local | Sem inventar porta pública ou substituir o BFF |
| Dados | PostgreSQL Suite, estado protegido PIX e armazenamento TurboBox distintos | Não restaurar um como se fosse o outro |

A comparação Linux encontrou **40 registros DNS iguais ao baseline** e configuração remota do túnel sem diferenças. O baseline já continha o IP novo no A de `db`. O metadado `modified_on` não demonstra quem editou nem o valor anterior. A Suite usa `app`, não `db`, para licenciamento.

O destino loopback identifica a própria máquina onde o conector alcança o serviço. Se conector e aplicação permanecem no mesmo host, mudar o IP LAN não exige trocar `127.0.0.1` pelo novo IP público/privado. Se existir outro ingress realmente apontando para IP antigo, investigar somente esse destino comprovado, preservando os demais.

Não abrir 5187/5190/5191/5432 à Internet, substituir o CNAME de `app` por um A de origem, remover Cloudflare, editar hosts dos clientes ou fixar os IPs anycast da borda.

## 4. Linha do tempo do início à confirmação do proprietário

Todos os eventos abaixo são de setembro de 2026; “local” significa UTC−3. Datas/horários de coleta não são automaticamente horários da causa ou da correção.

| Momento local / UTC | Evento e evidência |
| --- | --- |
| 07/09 17:14:41 / 20:14:41 | Início de Nginx, PIX e API após a manutenção, conforme retorno Linux |
| 07/09 17:15:24–26 / 20:15:24–26 | Conexões do túnel que ainda estavam ativas na retomada |
| 08/09 01:17:10 / 04:17:10 | Última sessão real IPv6 HTTP 200 identificada no levantamento inicial; ocorreu depois do boot |
| 08/09 07:48–07:58 / 10:48–10:58 | Sondas Windows alcançaram API por IPv4; IPv6 falhou antes de TCP/TLS, inclusive em controles; sonda .NET normal expirou, variante IPv4 respondeu |
| 08/09 08:10–08:21 / 11:10–11:21 | Executor Linux confirmou serviços/túnel; reproduziu falha IPv6 e tentou mitigação isolada de DNS, recusada com código 9227; nenhuma alteração persistida |
| 08/09 09:03 / 12:03 | Windows: Suite automático/IPv4 responderam 405; IPv6 expirou em 4,020 s; PIX respondeu 200. Eram sondas, não login |
| 08/09 09:37:39 / 12:37:39 | Publicado handoff de retomada `ce8d644`, com IP informado e aceite real pendente |
| 08/09 09:52–09:53 / 12:52–12:53 | Linux: IPv6 e controles voltaram a responder; sonda .NET padrão e variante IPv4 também responderam; nenhum delta de produção aplicado pelo executor |
| 08/09 09:54:18 / 12:54:18 | Concluída a nova compilação Windows de teste, sem alterar a lógica de rede/licenciamento |
| 08/09 09:58:15 / 12:58:15 | Publicado retorno Linux `862c087`, informando transporte recuperado e login Windows ainda pendente naquele recorte |
| 08/09 10:00:51–54 / 13:00:51–54 | Nova sonda deste Windows confirmou automático/IPv4/IPv6 e controle Cloudflare funcionando |
| 08/09 10:13:53 / 13:13:53 | Criação do processo da nova cópia de teste, identificada posteriormente por `Win32_Process`; a tela de login foi confirmada pelo controle de janela |
| Após abrir o programa, antes desta consolidação | Proprietário respondeu “agora foi”. Confirmação de entrada recebida no chat; o horário exato do sucesso autenticado não foi capturado |

Na conferência de preparação desta entrega, iniciada às **10:26:39 locais**, o processo correspondia ao caminho do novo pacote e tinha CreationDate `2026-09-08T10:13:53.916267-03:00`. O intervalo aproximado 13:13:53–13:27 UTC pode orientar uma busca inicial nos logs; **não é o timestamp comprovado do login**. O processo estar aberto não comprova isoladamente sessão/heartbeat.

## 5. Evidências técnicas e o alcance de cada uma

### 5.1 Durante a falha

- A resolução .NET observada entregou IPv6 antes de IPv4. Havia endereços IPv6 globais e rota padrão; a afirmação antiga de “não existe rota IPv6” foi corrigida. Ter rota anunciada não significa ter saída utilizável.
- TCP/TLS IPv6 falhou em Windows e Linux, também para Google/Cloudflare em lotes anteriores. IPv4 respondeu. O salto exato entre rede, roteador, operadora e borda não foi localizado.
- Uma sonda de licença fictícia retornou `LICENSE_NOT_FOUND`; pela implementação revisada, ocorreu leitura de licença no PostgreSQL naquele instante. Isso não prova integridade de todos os dados, validade da licença real nem gravação de sessão.
- No lote Windows das 09:03, GET Suite retornou 405 em 0,566 s no automático e 0,400 s em IPv4; IPv6 expirou em 4,020 s. PIX retornou 200 em 0,601 s.
- O servidor posteriormente correlacionou esses GETs Windows às 12:03:04 UTC e o health PIX às 12:03:09 UTC. Não reutilizar essas entradas como se fossem o EXE.

### 5.2 Recuperação observada no Linux

Às 09:52, Suite automático/IPv4/IPv6 respondeu 405 em 0,343/0,342/0,288 s; os controles Google e Cloudflare responderam por ambas as famílias. Readiness Suite, conteúdo e gateway respondeu, com 902 itens esperados no baseline de conteúdo. Health PIX respondeu 200 com JSON `ready:true`.

Às 09:53, `SuiteTlsCheck` em **.NET 8.0.30 no Linux** respondeu aproximadamente 0,76 s no modo padrão e no comparativo IPv4. Foram observados `TLS_POLICY_ERRORS=None`, `CURRENT_AUTHORITY_PIN_MATCH=True` e HTTP 400 `JSON_INVALID` para POST `{}`. O POST inválido não abriu sessão/ativou licença. A variante IPv4 foi restrita ao processo da sonda, sem mudar ambiente de serviço ou cliente. Esse teste não é uma execução do cliente Windows .NET 10.

Não houve reinício/reload/deploy pelo executor. A API mantinha o release `es-reopen-efaf1d3-20260905`, InvocationID `07f9f1884c0846f99a9d3321bc7dba7d` e SHA-256 da DLL `e10bcf191c7b1c4b030427713b848a8e89af51517483d319b5979cd9ea7b07ef`.

### 5.3 Recuperação observada neste Windows

Lote de **13:00:51–13:00:54 UTC**, `curl.exe 8.13.0`, Schannel, sem proxy/credenciais, TLS padrão, sem redirecionamento, connect timeout 4 s e total máximo 7 s. User-Agent: `LZ-Handoff-Windows-Retorno/20260908-1000`.

| Início UTC | Teste | HTTP / saída curl | Família/destino conectado | Total |
| --- | --- | --- | --- | --- |
| 13:00:51.2598544 | GET Suite automático | 405 / 0 | IPv6 `2606:4700:3036::6815:3a6` | 1,426566 s |
| 13:00:52.7141186 | GET Suite IPv4 | 405 / 0 | IPv4 `172.67.130.243` | 0,316329 s |
| 13:00:53.0475565 | GET Suite IPv6 | 405 / 0 | IPv6 `2606:4700:3036::6815:3a6` | 0,341380 s |
| 13:00:53.4062742 | GET Cloudflare IPv6 | 200 / 0 | IPv6 `2606:4700::6810:7c60` | 0,808556 s |

Suite aqui significa `https://app.lzgames.com.br/v1/suite/challenges`; o controle foi `https://www.cloudflare.com/`. GET 405 é compatível com a rota que exige POST. O corpo foi descartado; não houve coleta de headers/CF-RAY neste lote. Não inventar correlações. Os endereços conectados são da borda, não o IPv4 público da origem.

**Conclusão:** o timeout anterior não se reproduziu nesse lote. Isso não garante estabilidade futura, não mede todas as redes e não comprova login por si só. A confirmação posterior do proprietário é a evidência adicional de entrada no programa.

## 6. O que foi tentado, o que mudou e o que não causou uma correção comprovada

| Ação | Resultado | Interpretação correta |
| --- | --- | --- |
| Inspeção de código, DNS, rede, serviços e logs | Camadas separadas e falha IPv6 reproduzida | Diagnóstico; não é mudança de produção |
| PATCH isolado `settings.ipv4_only=true` em `app` | Cloudflare HTTP 400, código 9227; recurso indisponível para a zona; releitura sem mudança | Não foi uma mitigação aplicada; não repetir com outro token como se fosse só autenticação |
| Informação/edição de IP relatada pelo proprietário | Linux confirmou saída `187.21.51.38`; A de `db` já tinha esse valor no baseline | Não prova alteração de ingress nem causa da recuperação da Suite |
| Recompilação de comportamentos de download | Build/gates aprovados, nova cópia de teste | Não incluiu patch de rede/IP/licenciamento; não demonstra que recompilar resolveu o timeout |
| Nova verificação Linux/Windows | Transporte voltou a responder | Recuperação observada, com causa e instante exatos não determinados |
| Abertura do pacote e tentativa do proprietário | “agora foi” | Entrada confirmada pelo usuário; falta completar a homologação operacional |

Não foram executados pelo atendimento: migração/restauração/limpeza do banco, reativação em massa, rotação de chaves, troca de certificado/pin, abertura de portas, alteração global de IPv6, reinstalação de serviços ou reinício do servidor/roteador como correção. Não atribuir ao atendimento uma intervenção externa não auditada.

## 7. Executável efetivamente aberto e diferença entre compilações

Pacote aberto para o teste atual:

```text
J:\TURBORAMA SUITE COMPILAÇÃO\TESTE-SUITE-20260908-0950\pacote\Turborama-2.0.2-win-x64-44c936ace6e8-UNSIGNED-NOT-FOR-DISTRIBUTION\Turborama.exe
```

- Versão-base 2.0.2, Windows x64, self-contained; SDK de build .NET 10.0.400.
- Commit-base `44c936ace6e8645edbfe9b15aeb093da35408504`, **com alterações locais não commitadas**, registradas como `dirty: true` no manifesto.
- Build UTC `2026-09-08T12:53:40.7290901Z`; conclusão do pipeline às 12:54:18 UTC.
- EXE com 180.793.078 bytes; SHA-256 **`054EC236FB72209D77A6E509D93CD99025EAE85578598FBB17F5AD2033687ED6`**, conferido no pacote e novamente antes da abertura.
- Authenticode `NotSigned`: **somente teste, não versão liberada para distribuição**. Autoridades públicas de licença e conteúdo incorporadas e validadas, sem bypass.
- Manifesto `RELEASE-MANIFEST.json` e SBOM `Turborama.spdx.json`; 1.050 arquivos inventariados no pacote.
- Antes da abertura não havia processo Turborama encontrado. A janela do caminho exato acima foi selecionada, sem substituir a instalação anterior ou iniciar outra cópia antiga.
- O proprietário realizou o teste de entrada. Não foi automatizado preenchimento de OTP/credenciais ou alteração de licença.

O EXE com SHA `577E5DDC...` da pasta `VALIDACAO-DOWNLOADS-20260908` é uma compilação de teste **anterior**, e o SHA `9f6d4dbf...` do incidente inicial identifica outra ainda mais antiga. Não selecionar pacote apenas por versão “2.0.2” ou nome “final”. Conferir caminho e SHA.

O build passou com zero erros e zero avisos, testes integrados do catálogo 22/902, mídia, UI, licenciamento, retomada/extração e comportamento novo. Os casos específicos de habilitar diretório case-sensitive e criar symlink tiveram skips por permissões do Windows; não declarar esses casos aprovados. As três passagens de validação de pacote/autoridades/hashes passaram. Os arquivos locais alterados foram preservados.

### 7.1 Limite das alterações de download já presentes

O comportamento novo é acionado por baixar/continuar/tentar novamente nas categorias abrangidas, com raiz `TURBORAMA`. Os **jogos** PS4/PS5/Xbox One/Xbox Series permanecem no fluxo anterior. Formatos mantidos ou extraídos seguem a política da categoria; a árvore de pacote é preservada no fluxo novo, sem sobrescrever silenciosamente conteúdo diferente. Não foram mudados o domínio, as autoridades, a autenticação ou o transporte de licença.

A biblioteca antiga ainda tem sua lógica em `TruboRoms\roms`; indexar automaticamente os novos destinos não integrou essa entrega. Login funcionando não valida transferência, integridade, publicação ou extração. O gateway vigente usa redirecionamento autorizado HTTP 307 no modelo direto; readiness e número esperado de itens não provam disponibilidade nem bytes de cada arquivo. Não publicar grants/URLs de origem nem chamar hash local de digest oficial do fornecedor.

## 8. Procedimento do início ao fim se o erro voltar

Os passos seguintes são um **runbook condicional**, não ações executadas agora nem uma receita de reset. Com o acesso funcionando, não provocar mudanças preventivas baseadas apenas nos timeouts antigos.

### Etapa A — registrar o sintoma e preservar o estado

1. Registrar data/hora com fuso, texto exato, etapa da falha e versão/caminho/SHA do EXE. Distinguir “não executa”, “não abre sessão” e “entrou, mas catálogo/download falhou”.
2. Confirmar se o cliente e servidor compartilham rede/operadora; não deduzir duas redes independentes a partir de dois equipamentos.
3. Não apagar identidade CNG/TPM, cache de licença, dados, sessões ou chaves. Não pedir tokens/OTP/chaves no chat.
4. Conferir uma instância por pacote e evitar atalhos antigos. Encerramentos com download ou trabalho ativo precisam ser coordenados com o proprietário.

Exemplos Windows de leitura; não publicam conteúdo de licença:

```powershell
Get-Date -Format o
Get-CimInstance Win32_Process -Filter "Name = 'Turborama.exe'" |
    Select-Object ProcessId, ExecutablePath, CreationDate
curl.exe --version
```

Depois de identificar o caminho real, verificar SHA com `Get-FileHash -LiteralPath <caminho-absoluto-confirmado> -Algorithm SHA256` e comparar ao manifesto correspondente. O marcador entre `<...>` precisa ser substituído por um caminho confirmado; não copiar como comando pronto.

### Etapa B — testar o domínio nas famílias IP, sem autenticar

No PowerShell, uma pequena rodada controlada por família, sem alterar configuração:

```powershell
$taskChecks = @(
    @{ Name = 'suite-auto'; Url = 'https://app.lzgames.com.br/v1/suite/challenges'; Family = '' },
    @{ Name = 'suite-ipv4'; Url = 'https://app.lzgames.com.br/v1/suite/challenges'; Family = '-4' },
    @{ Name = 'suite-ipv6'; Url = 'https://app.lzgames.com.br/v1/suite/challenges'; Family = '-6' },
    @{ Name = 'controle-ipv4'; Url = 'https://www.cloudflare.com/'; Family = '-4' },
    @{ Name = 'controle-ipv6'; Url = 'https://www.cloudflare.com/'; Family = '-6' }
)
foreach ($taskCheck in $taskChecks) {
    $taskStart = [DateTimeOffset]::UtcNow.ToString('o')
    $taskArgs = @('--noproxy', '*', '--connect-timeout', '4', '--max-time', '7',
        '--silent', '--show-error', '--user-agent', 'LZ-Suite-Diagnostico/20260908',
        '--output', 'NUL', '--write-out',
        "utc=$taskStart teste=$($taskCheck.Name) http=%{http_code} peer=%{remote_ip} tcp=%{time_connect} tls=%{time_appconnect} total=%{time_total}\n")
    if ($taskCheck.Family) { $taskArgs += $taskCheck.Family }
    $taskArgs += $taskCheck.Url
    & curl.exe @taskArgs
    "curl_exit=$LASTEXITCODE"
}
```

Interpretação: 405 de GET na rota de challenge é esperado; `000`/saída 28 é ausência de resposta no timeout, não status da API. `time_appconnect` é acumulado até TLS, não duração isolada do handshake. O código acima não recolhe headers/corpo nem abre sessão. Não usar `-k`, não editar hosts e não fixar IPs da borda.

Repetir a comparação no Linux e em rede externa independente quando necessário. Só reprovar IPv6 do serviço a partir de redes que comprovem IPv6 funcional nos controles. curl, .NET Linux e EXE Windows não são testes equivalentes. Se necessário, usar sonda temporária equivalente ao handler do cliente, conservando HTTPS/pin/revogação e sem alterar o produto.

### Etapa C — conferir origem, túnel e dependências por acesso autorizado

No Linux, registrar resultados sensíveis somente em diretório protegido. Leituras iniciais:

```bash
date -u --iso-8601=seconds
uptime -s
timedatectl status
ip -br address
ip -4 route show
ip -6 route show
findmnt --real
df -hT
systemctl --failed --no-pager
systemctl show turborama-suite-api.service turborama-pix.service \
  turborama-suite-admin.service turborama-suite-content-gateway.service \
  -p Id -p ActiveState -p SubState -p MainPID -p InvocationID \
  -p ExecMainStartTimestamp -p NRestarts -p Result
curl --noproxy '*' --connect-timeout 3 --max-time 8 -sS -i http://127.0.0.1:5187/v1/health
curl --noproxy '*' --connect-timeout 3 --max-time 8 -sS -i http://127.0.0.1:5190/ready
curl --noproxy '*' --connect-timeout 3 --max-time 8 -sS -i http://127.0.0.1:5190/ready/content
curl --noproxy '*' --connect-timeout 3 --max-time 8 -sS -i http://127.0.0.1:5191/ready
```

Confirmar unidade/cluster efetivos, montagens e caminho/hash real da DLL. Processo ativo ou health 200 não comprova banco/sessão. Não tratar HTML do portal em `/health` público como health da API ou como prova automática de proxy errado. Não expor endpoints locais para facilitar monitoramento.

Conferir privadamente o DNS e a configuração **efetivamente carregada** do túnel: gestão remota/local, todos os conectores, cada hostname, destino e ordem. Um YAML local inativo não valida configuração remota. Comparar com backup; não recriar túnel nem imprimir token/credenciais. Rever `nginx -T`, units e logs apenas no ambiente protegido, pois podem conter segredos; compartilhar somente resumo sanitizado.

Se a falha estiver no banco, identificar cluster, banco, schema e papel de aplicação reais e começar com consultas autorizadas somente leitura/timeout. Uma leitura administrativa não valida automaticamente os grants do processo. Não executar migrations antigas, restaurar cluster ou ampliar privilégios para tornar uma sonda verde.

### Etapa D — acompanhar uma tentativa real e escolher a camada

Coordenar uma tentativa legítima com a identidade existente, sem nova ativação desnecessária. Registrar janela UTC e correlacionar processo, borda quando acessível, Nginx e API; usar CF-RAY/correlação quando realmente existirem. Ausência de log da API não prova, sozinha, que nada chegou à Cloudflare. Logging restrito/amostrado e falha antes de HTTP devem ser anotados.

| Resultado observado agora | Reparação a avaliar, somente se comprovada | Aceite exigido |
| --- | --- | --- |
| Sem TCP/TLS; controles IPv6 também falham apenas na rede afetada | Corrigir causa na rede administrada/roteador/RA/delegação/operadora, com acesso e rollback; ou encaminhar evidências ao responsável | Controles e domínio funcionando na mesma rede, mais tentativa real |
| Cloudflare recebe, mas conector/origem não | Corrigir regra/conector/destino exato divergente, preservando restantes | Todos os hostnames dependentes e EXE real |
| Nginx recebe, mas API não | Corrigir location/método/header/upstream comprovado, sem substituir vhost inteiro | Portal, Suite, ES e conteúdo preservados |
| API falha localmente após boot | Restaurar disponibilidade da dependência/montagem/configuração existente demonstrada | Mesmo armazenamento/chaves/release e fluxo completo |
| API recebe e retorna erro de protocolo/assinatura/SQL | Investigar status, contexto, exceção e SQLSTATE sanitizados; aplicar correção específica autorizada | Respostas válidas, negações corretas e isolamento |
| Sessão funciona, catálogo/download falha | Tratar conteúdo, autorização, gateway, disponibilidade ou destino local na etapa demonstrada | Catálogo/arquivo/retomada/extração, sem recriar licença |
| Transporte voltou a funcionar | Testar EXE existente e registrar recuperação/intermitência | Não aplicar mitigação antiga como se a falha ainda estivesse presente |

### Etapa E — preparar mudança mínima e reversão, quando realmente necessária

Identificar antes da execução: causa sustentada por evidência; arquivo/campo/unidade exatos; estado anterior; consumidores afetados; backup protegido; autorização e janela; validação; gatilho de rollback. Mudanças de rede exigem acesso local/fora de banda para não perder a administração.

Não há nesta entrega um patch de produção a reaplicar. Se surgir erro de Nginx comprovado, validar sintaxe antes de um reload autorizado. Se surgir erro de serviço, restaurar/reiniciar somente o componente necessário e autorizado, sem reiniciar tudo. Não oferecer reset de rede, renovação WAN, desligamento de IPv6 ou reboot como receita genérica.

Rollback reverte **apenas o delta aplicado**. Não restaurar snapshot de banco por regressão HTTP, pois isso pode apagar operações novas de outros clientes. Não executar rollback histórico de múltiplos serviços para uma alteração isolada. Nunca substituir dados montados por banco vazio.

### Etapa F — validar e encerrar com limites claros

Primeiro confirmar a entrada no EXE correto. Depois cumprir a matriz da seção 10, registrando cada teste aprovado, falho ou pendente. Observar sessões/heartbeat e disponibilidade por um intervalo acordado; não estabelecer garantia de estabilidade por uma única chamada.

Se houver recorrência, registrar nova janela no mesmo documento e reabrir a investigação da camada observada. Não descartar a evidência de recuperação nem presumir causa idêntica. Não criar monitoramento, alertas, testes de carga ou notificações externas sem autorização/destino definidos.

## 9. Recompilar o cliente, somente quando necessário para a entrega

**Recompilar não é a correção estabelecida para este incidente.** Neste atendimento foi pedido um pacote para testar os comportamentos de download já alterados. Se a rede falhar em versão válida, diagnosticar a rede antes de reconstruir ou trocar identidade.

Fonte local usada: `J:\TURBORAMA SUITE COMPILAÇÃO\TRUBORAMA-SUITE-v2.0.2-clone`. Preservar seu worktree. Um clone remoto apenas de `44c936ace6e8645edbfe9b15aeb093da35408504` **não contém os 12 arquivos locais alterados/novos desta entrega**. Antes de transferir manutenção, versionar/revisar essas alterações por tarefa autorizada; este push é documental no servidor, não publicação do código do cliente.

O procedimento de compilação já validado usa `tools/Build-Production.ps1`, com:

1. Windows x64, PowerShell 7, Git oficial e SDK .NET **10.0.400**, no J:.
2. Pasta de saída nova, sem sobrescrever pacotes anteriores; temporário curto `J:\TURBORAMA-TEMP` existente e sem redirecionamentos, usado em TEMP/TMP somente no processo da compilação.
3. `-UnsignedStaging`; `-AllowDirty` apenas para esta árvore local explicitamente identificada como alterada, nunca para declarar reprodução exata/publicação de produção.
4. Quatro arquivos públicos originais e seus SHA-256 fixados, listados abaixo. Não aceitar outro arquivo simplesmente atualizando o hash.
5. Gate de fonte, restore locked, build Release, verificadores, validação das autoridades, publish self-contained win-x64, SBOM/manifesto e gates do pacote.
6. Verificar SHA do EXE, assinatura declarada, manifesto e conjunto completo de assets antes de abrir. Não misturar EXE de um pacote e assets de outro.

| Arquivo em `authority/public` | SHA-256 aprovado |
| --- | --- |
| `suite-authority-envelope.json` | `20F7F066B654AAD700C4733C9B011495A2BB9B52E7A8B3A77E806CDEDEBFA3E6` |
| `suite-authority-issuer.spki.der` | `9BA572CC64CCFD9DCADA0699AB5E4F43E4662F84C1A82908A1125AA56C987B3A` |
| `content-authority-envelope.json` | `56E7A1BD100E5B5A9CD1109C0B90EDFCAFAF6C1497AE3113551684D872A0BA07` |
| `content-authority-issuer.spki.der` | `65631E0AAA9EFB75991098F7A68DDA462004E5BBC183AE6BF4FF9256397A8DC5` |

O script local usado ficou em `J:\TURBORAMA SUITE COMPILAÇÃO\TESTE-SUITE-20260908-0950\compilar-teste.ps1`; ele recusa saída já existente. Não apagar essa saída para reutilizar o nome: preparar outra pasta e o procedimento correspondente. O [handoff de construção do cliente](https://github.com/luziellacerda/TRUBORAMA-SUITE/blob/44c936ace6e8645edbfe9b15aeb093da35408504/docs/HANDOFF-CONSTRUCAO-HUMANO-V2.0.2-20260905.md) contém os oito parâmetros de caminho/hash e o roteiro do pipeline; as decisões de download posteriores deste atendimento prevalecem sobre a descrição histórica de extração daquele documento.

A tentativa anterior com temporário excessivamente longo falhou por caminho de dependência; o temporário curto no J: permitiu a compilação, sem alterar a configuração global do Windows. Isso é um problema de **build**, não a causa demonstrada do timeout de licença. O pipeline limpa somente seu diretório temporário isolado; não autoriza limpeza de discos, bibliotecas ou compilações anteriores.

## 10. Preservação do ecossistema e matriz de aceite atual

Preservar licenças, dispositivos, chaves CNG/TPM dos clientes, estado PIX, PostgreSQL Suite, dados TurboBox, sessões de terceiros, inventários, histórico, outbox, autoridades, peppers, certificados/pins e credenciais systemd. Não gerar pagamentos, OTPs, grants de produção em massa ou WhatsApp como diagnóstico genérico.

Preservar a correção de reabertura ES e o cabeçalho **`X-TurboRama-Client: EMULATIONSTATION`** exatamente no cliente ES; a Suite normal não deve recebê-lo como atalho. Manter isolamento entre produtos/clientes e permissões mínimas. NAT/CGNAT não transforma um IP público em identidade de licença.

| Frente | Estado neste fechamento documental | O que ainda verificar |
| --- | --- | --- |
| Rede Linux | Recuperação IPv4/IPv6 e controles registrada pelo servidor | Estabilidade e causa da intermitência, se necessária/recorrente |
| Rede deste Windows | Automático/IPv4/IPv6 e controle IPv6 aprovados às 10:00 | Outras operadoras não foram homologadas |
| Inicialização/entrada | Pacote aberto e login confirmado pelo proprietário | Correlacionar tentativa autenticada no servidor sem confundir com sondas |
| Sessão/catálogo | Entrada informada; não foi feita auditoria detalhada posterior dos eventos | Challenge/sessão/heartbeat e carregamento do catálogo com evidência apropriada |
| Downloads/extração | Testes locais/offline aprovados | Um arquivo de teste autorizado, pausa/retomada, publicação em TURBORAMA e extração prevista |
| ES/coexistência | Runtime e contratos preservados pelo servidor | Abrir/sair/reabrir e isolamento Suite/ES/cliente A/B |
| PIX/portal/painéis | Health PIX e baseline de rotas preservados; sem mudança de produção | Fluxos funcionais seguros, Access/autenticação/CSRF e zero cobrança real |
| Outros sites/timers | Configuração compartilhada preservada no retorno Linux | Testes proporcionais se uma mudança futura os afetar |
| Reboot/persistência | Nenhuma configuração nova implantada nesta investigação | Reboot de aceite não executado; só em janela autorizada |

Abertura da loja normalmente sucede sessão autorizada e leitura inicial de catálogo, conforme o código; porém o “agora foi” do usuário não é uma captura detalhada de todas essas etapas. Registrar a confirmação de entrada sem preencher automaticamente os demais itens da matriz. Da mesma forma, prontidão de 902 itens não significa que todos estejam disponíveis para baixar.

## 11. Referências, branches e provas preservadas

Não implantar a `main` deste repositório como se fosse toda a produção Suite: ela contém código PIX legado e documentação nova. O baseline real de API do retorno continua sendo o release `es-reopen-efaf1d3-20260905`. Confirmar runtime atual antes de qualquer intervenção futura.

- [Incidente original e primeiro retorno — d4918a3d](https://github.com/luziellacerda/Servidor-pix/blob/d4918a3d0735603dce9205fa580f29faa8e4a4b5/HANDOFF-INCIDENTE-CONECTIVIDADE-POS-TROCA-PLACA-SERVIDOR-20260908.md).
- [Retomada Windows publicada — ce8d6443](https://github.com/luziellacerda/Servidor-pix/blob/ce8d6443fed5df9060279ce6e95af8c4401cd876/HANDOFF-RETOMADA-TUNEL-IP-PUBLICO-E-ACEITE-SUITE-20260908.md).
- [Retorno Linux com transporte recuperado — 862c0873](https://github.com/luziellacerda/Servidor-pix/blob/862c0873baff5e2c6d4a7778ebf7c6cf868ac49e/HANDOFF-RETOMADA-TUNEL-IP-PUBLICO-E-ACEITE-SUITE-20260908.md).
- [Manual completo de funções/operação — 862c0873](https://github.com/luziellacerda/Servidor-pix/blob/862c0873baff5e2c6d4a7778ebf7c6cf868ac49e/HANDOFF-FUNCOES-E-OPERACAO-SERVIDOR-PIX-SUITE-20260908.md).
- [Contratos de reabertura ES — eb522526](https://github.com/luziellacerda/Servidor-pix/blob/eb522526547be876982f3fabb79f59fefb8fb702/docs/suite/HANDOFF-EMULATIONSTATION-ROTAS-COMPARTILHADAS-20260905.md).

O retorno Linux está na branch `codex/repair-suite-connectivity-20260908` e referencia a PR #1. Sua leitura e a publicação deste consolidado não significam merge dessa PR, execução de deploy ou substituição de código do servidor.

Evidências administrativas brutas permanecem no diretório protegido do Linux indicado no retorno: `/home/lz-servidor/evidence/reparo-suite-20260908/retorno-tunel-20260908-0950/`. O resumo do pacote Windows está em `C:\Users\Admin\Documents\Codex\2026-09-07\pco\outputs\compilacao-turborama-teste-20260908-0954.md`; ele retrata a entrega do build, antes da abertura posterior registrada neste consolidado.

Não publicar tokens, DSNs, cookies/Authorization, OTPs, chaves privadas, proofs, grants, URLs temporárias/privadas, backups ou dumps de clientes. Usar logs apenas com janela, campos e acesso mínimos necessários; resumir evidência sanitizada no Git.

## 12. Registro de continuidade e conclusão

| Campo | Estado de referência |
| --- | --- |
| Sintoma original | Indisponibilidade genérica ao entrar, após manutenção relatada |
| Falha comprovada nas sondas | Timeout IPv6 anterior ao HTTP; IPv4 respondia |
| Componente responsável / causa-raiz | Não determinado; não há prova causal para atribuir a placa, edição de IP, roteador ou operadora específicos |
| Recuperação técnica | Linux 09:52–09:53; Windows 10:00; sem delta de produção aplicado pelo executor |
| Entrada real | Confirmada pelo proprietário com “agora foi”, após abertura do pacote SHA `054EC236...` |
| Mudança de rede/licença no cliente para corrigir incidente | Nenhuma |
| Mudança efetiva de produção neste atendimento | Nenhuma registrada pelos executores; tentativa DNS recusada |
| Pedido atendido agora | Novo handoff consolidado publicado; não um novo reparo/deploy |
| Pendências | Correlação autenticada, estabilidade, download/extração reais e restante da matriz |
| Próxima atualização | Acrescentar aqui horário UTC, teste, resultado, fonte, alteração autorizada se houver e rollback; não reaplicar o histórico inteiro |

**Conclusão operacional:** o proprietário conseguiu entrar na Turborama depois que a conectividade voltou a responder. A forma segura de atender uma recorrência é identificar a camada com medições atuais, preservar o túnel e os dados, corrigir somente um defeito demonstrado e validar no EXE certo. Este caso não fornece uma alteração de produção comprovada para ser repetida automaticamente.
