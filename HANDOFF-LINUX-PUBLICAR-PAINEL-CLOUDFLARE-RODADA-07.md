# HANDOFF — publicar painel com Cloudflare Access — rodada 07

Data: 2026-08-08

## Instrução para a conversa no Linux

O usuário está no servidor Linux. Leia este arquivo inteiro antes de agir. Continue do retorno
sanitizado da rodada 06. Não reinicie o projeto, não reinstale a aplicação, não use comandos Windows,
não peça credenciais pelo chat e não publique valores de arquivos privados. Execute toda a rodada e
gere o retorno obrigatório ao terminar.

O usuário não deve receber uma sequência de comandos Linux para executar manualmente. A conversa no
Linux deve conduzir e verificar as operações, pedindo interação do usuário apenas quando for
indispensável autenticar no painel Cloudflare ou digitar uma senha em entrada oculta.

## Objetivo único

Publicar `painelpix.lzgames.com.br` no **túnel Cloudflare existente**, apontando exclusivamente para
`http://127.0.0.1:5187`, exigir validação do token Cloudflare Access no `cloudflared` e, somente depois
de provar as duas barreiras, ativar o painel administrativo da rodada 06.

Esta rodada não altera código, pacote, banco, preços, licenças, máquinas nem Mercado Pago.

## Estado confirmado antes desta rodada

- aplicação instalada: rodada 06, commit
  `102b8f0b6a436a999885188d0683a63d57755180`;
- DLL instalada, SHA-256:
  `014182be32bfa540a0cde9edbc988519728c79249db9402e9ee6cbf3076f92ee`;
- serviço `turborama-pix` ativo/habilitado e escutando somente em `127.0.0.1:5187`;
- API `https://pix.lzgames.com.br/v1/health` responde `200`;
- todos os caminhos `/admin*` no hostname da API respondem `404`;
- `TURBORAMA_ADMIN_PUBLIC_HOST` está ausente, efeito equivalente a vazio;
- painel local e externo estão desativados;
- clientes/licenças/máquinas: `0/0/0`;
- nenhuma credencial Mercado Pago e nenhuma cobrança;
- nginx, cloudflared, MariaDB, site e portas da empresa preservados;
- backup da rodada 06:
  `/var/backups/turborama-pix/round06-20260807T173720Z`;
- retorno-base: `RETORNO-LINUX-RODADA-06.md`.

## Cloudflare Access criado no Windows

Em 2026-08-08, o painel do Cloudflare confirmou a aplicação:

- nome: `painelpix`;
- destino integral: `painelpix.lzgames.com.br`;
- tipo: auto-hospedado;
- política: `Turborama`;
- ação: `Allow/Permitir`;
- regra `Include`: e-mail exato do proprietário;
- regra `Require`: membro da conta Cloudflare selecionada;
- duração da política: 30 minutos;
- provedor: somente Cloudflare;
- autenticação instantânea: ativada;
- Cloudflare One Client: desativado;
- App Launcher e acesso sem cliente/Browser Isolation: desativados.

Não repetir o e-mail no retorno. Antes de publicar a rota, abra a aplicação apenas para auditoria e
confirme esses atributos. Se houver `Everyone`, `Bypass`, curinga, exceção de caminho, outro provedor,
App Launcher clientless ou ausência da regra de conta, pare sem alterar servidor/túnel e retorne
`BLOQUEADO_POR_ACCESS_DIVERGENTE`.

## Proteções absolutas do sistema da empresa

É proibido alterar:

- portas/regras `3302`, `3306`, `13306` e `23306`;
- UFW, iptables, ip6tables, nftables, NAT ou roteador;
- MariaDB, `bind-address`, bancos ou usuários;
- site, arquivos do site, nginx, aplicações LZGames ou hostnames existentes;
- regra da API `pix.lzgames.com.br` e rotas `/v1/*`;
- serviço local `127.0.0.1:5187` para qualquer endereço público;
- modo de gerenciamento do túnel, token, credencial ou identidade do túnel existente;
- regras existentes do túnel, inclusive ordem e catch-all, salvo inserir a nova regra administrativa
  imediatamente antes do catch-all quando o túnel for gerenciado localmente.

Não abrir portas. Não criar outro túnel. Não substituir o arquivo inteiro do túnel por modelo. Não
converter túnel local em remoto ou remoto em local. Não instalar painel no nginx. Não reutilizar o
hostname da API para administração.

## Regra de segredos

Nunca exibir ou registrar em chat, retorno, histórico ou Git:

- senha administrativa ou hash PBKDF2 completo;
- cookie, token CSRF, `CF_Authorization` ou `Cf-Access-Jwt-Assertion`;
- token/API token/tunnel token/credencial do Cloudflare;
- conteúdo de `server.env`;
- chaves do estado, Mercado Pago, Client Secret ou Access Token;
- código de ativação ou chave privada/pública de máquina.

O `teamName` e o AUD da aplicação devem ser obtidos no painel/configuração autorizada e usados apenas
na configuração protegida do túnel. No retorno, informe somente que foram configurados e, se for
necessário correlacionar, registre no máximo SHA-256 ou prefixo curto não reutilizável, nunca o token
Access de uma sessão.

## Fase A — auditoria somente leitura

1. Ler integralmente `RETORNO-LINUX-RODADA-06.md` e confirmar o último estado.
2. Confirmar o commit instalado e o SHA-256 do DLL; não executar `git pull` nem instalar pacote.
3. Verificar `turborama-pix`, nginx, cloudflared e MariaDB ativos/habilitados.
4. Confirmar health local e público, site principal e `404` nos cinco caminhos administrativos do
   hostname da API usados na rodada 06.
5. Confirmar listeners e preservar `3302`, `3306`, `13306`, `23306` e `5187`.
6. Calcular hashes de `state.json`, `server.env`, unidade `turborama-pix`, unidade/configuração real do
   cloudflared e configuração sanitizada de ingress antes de qualquer mudança.
7. Identificar sem alterar se o túnel é gerenciado localmente (`config.yml`) ou remotamente pelo
   Cloudflare. Não adivinhar e não converter o modo.
8. Inventariar a ordem de todas as rotas existentes somente por hostname e tipo de serviço, sem
   valores secretos. Confirmar o catch-all final.
9. Confirmar que não existe rota/DNS anterior para `painelpix.lzgames.com.br`.
10. Auditar a aplicação Access e exigir exatamente os atributos da seção anterior.
11. Obter de forma privada o `teamName` e o AUD da aplicação `painelpix`; validar que o AUD pertence a
    essa aplicação e não a outra.

Qualquer divergência deve parar a rodada antes de backup/alteração.

## Fase B — backup verificável

1. Criar backup root-only novo, com timestamp de rodada 07, contendo pelo menos:
   - configuração e unidade reais do cloudflared;
   - inventário sanitizado das rotas existentes;
   - `server.env`;
   - unidade `turborama-pix`;
   - aplicação e estado atuais;
   - registro sanitizado do estado DNS/Access anterior.
2. Aplicar proprietário `root:root`, diretório `0700` e arquivos privados `0600`.
3. Gerar manifesto SHA-256 e verificá-lo integralmente antes de modificar qualquer item.
4. Preparar rollback separado para:
   - remover somente a nova rota/DNS administrativa;
   - restaurar configuração cloudflared exata se gerenciada localmente;
   - remover somente as novas variáveis administrativas e restaurar `server.env` exato;
   - manter a rodada 06 instalada.

Se o backup ou o rollback não puderem ser demonstrados, parar com `BLOQUEADO_POR_BACKUP`.

## Fase C — adicionar rota com validação obrigatória do Access

A ordem é obrigatória. `TURBORAMA_ADMIN_PUBLIC_HOST` continua vazio durante toda esta fase.

### Túnel gerenciado remotamente

1. No túnel existente, adicionar uma única rota do tipo **Published application**:
   - hostname: `painelpix.lzgames.com.br`;
   - service URL: `http://127.0.0.1:5187`;
   - sem caminho;
   - **Protect with Access: ativado**;
   - aplicação/AUD: somente `painelpix`;
   - validação obrigatória do Access: ativada.
2. Não alterar, reordenar ou recriar outras rotas.
3. Confirmar que o DNS criado aponta ao mesmo túnel existente e permanece proxied.

### Túnel gerenciado localmente

1. Editar somente o arquivo real já usado pela unidade do cloudflared.
2. Inserir antes do catch-all uma única regra para:
   - `hostname: painelpix.lzgames.com.br`;
   - `service: http://127.0.0.1:5187`;
   - `originRequest.access.required: true`;
   - `originRequest.access.teamName`: organização atual confirmada;
   - `originRequest.access.audTag`: lista contendo somente o AUD da aplicação `painelpix`.
3. Não aplicar `originRequest.access` globalmente; ele pertence apenas à nova rota administrativa.
4. Criar o DNS pelo método autenticado já utilizado para o túnel existente, sem expor ou gerar novo
   token e sem tocar no DNS de `pix.lzgames.com.br`.
5. Validar sintaxe e ingress antes de recarregar somente cloudflared.

O parâmetro `access.required=true` é obrigatório: o cloudflared deve recusar tráfego L7 sem JWT
Access válido antes de encaminhar ao serviço local.

Se não for possível habilitar **Protect with Access** e provar a associação ao AUD correto, desfazer
somente a rota/DNS novo e parar com `BLOQUEADO_POR_VALIDACAO_ACCESS`.

## Fase D — comprovar Access antes de ativar o painel

Ainda com `TURBORAMA_ADMIN_PUBLIC_HOST` vazio:

1. Validar configuração e estado ativo do cloudflared.
2. Em sessão não autenticada/privada, acessar `https://painelpix.lzgames.com.br/admin`:
   - deve ocorrer desafio/redirecionamento do Cloudflare Access ou negação;
   - nunca pode aparecer login do TurboRama ou conteúdo do origin sem autenticação Access.
3. Autenticar pelo provedor Cloudflare com a identidade autorizada, sem copiar cookies/tokens.
4. Depois do Access, a resposta esperada do aplicativo é `404`, porque o hostname administrativo
   ainda está vazio. Esse `404` é a prova segura de rota + Access + origem antes da ativação.
5. Confirmar nos logs sanitizados que o cloudflared aceitou requisição com validação Access, sem
   imprimir JWT, cookies ou cabeçalhos.
6. Reconfirmar `https://pix.lzgames.com.br/v1/health` em `200` e `/admin*` da API em `404`.

Se uma sessão não autenticada alcançar o origin, executar rollback imediato da nova rota e retornar
`ROLLBACK_ROTA_ACCESS_EXECUTADO`.

## Fase E — criar segunda barreira e ativar o hostname exato

Executar somente após a Fase D inteira ser aprovada.

1. Escolher um nome administrativo não secreto e estável; usar `turborama-admin` se o proprietário
   não definir outro localmente.
2. Gerar senha forte em gerenciador de senhas ou entrada local privada. Não pedir nem repetir a senha
   no chat.
3. Executar o binário instalado com `--hash-admin-password` em terminal privado/PTY e redirecionar o
   resultado para arquivo temporário root-only, evitando que o hash apareça no output da conversa.
4. Confirmar duas entradas idênticas. Não guardar a senha em arquivo ou histórico.
5. Atualizar atomicamente `/etc/turborama-pix/server.env`, preservando todas as chaves existentes e
   adicionando/alterando somente:
   - `TURBORAMA_ADMIN_USERNAME`;
   - `TURBORAMA_ADMIN_PASSWORD_HASH`;
   - `TURBORAMA_ADMIN_PUBLIC_HOST=painelpix.lzgames.com.br`;
   - `TURBORAMA_ADMIN_KEY_DIRECTORY=/var/lib/turborama-pix/admin-data-protection` se ainda ausente.
6. Exigir `root:root` e modo `0600` no ambiente.
7. Exigir diretório Data Protection do usuário `turborama-pix`, modo `0700`, sem apagar chaves já
   existentes.
8. Remover com segurança o arquivo temporário do hash após a gravação e verificação sem eco.
9. Reiniciar somente `turborama-pix`; não reiniciar nginx, MariaDB ou site.
10. Aguardar readiness e verificar health local/público.

Se a aplicação não iniciar, restaurar somente `server.env`, manter a rota Access fechada ou removê-la
conforme segurança e conservar a rodada 06. Não reinstalar pacote.

## Fase F — validação das duas barreiras

1. Em sessão privada não autenticada, confirmar primeiro a tela/barreira Cloudflare Access.
2. Com a identidade autorizada, confirmar que `/admin` mostra o login próprio do TurboRama.
3. Entrar uma vez com o usuário/senha definidos localmente, sem expor senha, cookies, CSRF ou hash.
4. Confirmar painel vazio, sem clientes/licenças/máquinas e sem executar nenhuma ação administrativa.
5. Executar logout pelo painel e confirmar encerramento da sessão TurboRama.
6. Encerrar também a sessão Access pela URL oficial de logout sem capturar token.
7. Confirmar que novo acesso exige autenticação novamente conforme sessão de 30 minutos.
8. Repetir os cinco testes `/admin*` no hostname da API e exigir `404`.
9. Revalidar API, site, nginx, cloudflared, MariaDB, estado e portas protegidas.
10. Comparar hashes de `state.json`; deve permanecer idêntico. O hash de `server.env` deve mudar
    apenas pela inclusão prevista das variáveis administrativas.

## Proibições financeiras e comerciais

- não criar cliente, licença, máquina, código de ativação ou sessão;
- não cadastrar nem alterar preços;
- não solicitar, inserir ou validar credencial Mercado Pago;
- não criar order, QR, cobrança ou pagamento;
- não usar botões de alteração do painel nesta rodada.

## Critérios de parada

Parar e preservar/restaurar se:

- a aplicação Access divergir da configuração confirmada;
- o túnel, modo de gerenciamento ou configuração real não puder ser identificado;
- backup/manifesto/rollback falhar;
- for necessário substituir, apagar ou reordenar regra existente;
- Protect with Access/JWT obrigatório não puder ser habilitado para a nova rota;
- o AUD não puder ser associado inequivocamente à aplicação `painelpix`;
- sessão não autenticada alcançar o origin;
- API, site, nginx, cloudflared, MariaDB ou porta protegida forem afetados;
- `/admin*` deixar de retornar `404` no hostname da API;
- qualquer segredo aparecer em log/chat/retorno;
- algum passo tentar criar dados comerciais ou cobrança.

## Rollback obrigatório

### Falha antes de alterar `server.env`

- remover somente rota/DNS `painelpix.lzgames.com.br`;
- restaurar configuração cloudflared exata se local;
- validar serviços e manter rodada 06 com painel desativado;
- manter a aplicação Access criada, pois sem rota ela não expõe o origin.

### Falha após alterar `server.env`

- restaurar `server.env` exato do backup;
- reiniciar somente `turborama-pix` e confirmar painel desativado;
- remover/restaurar somente a rota administrativa;
- revalidar API, site, serviços e portas;
- não fazer rollback do binário rodada 06 se ele continuar saudável.

## Retorno obrigatório

Criar:

`/home/lz-servidor/turborama-download/Servidor-pix/RETORNO-LINUX-RODADA-07.md`

O retorno sanitizado deve incluir:

- data/hora e um resultado final:
  - `PAINEL_PUBLICADO_COM_ACCESS_E_LOGIN`;
  - `BLOQUEADO_POR_ACCESS_DIVERGENTE`;
  - `BLOQUEADO_POR_BACKUP`;
  - `BLOQUEADO_POR_VALIDACAO_ACCESS`;
  - `ROLLBACK_ROTA_ACCESS_EXECUTADO`;
  - `ROLLBACK_CONFIGURACAO_PAINEL_EXECUTADO`;
- commit e SHA-256 do DLL instalado, confirmando ausência de atualização de binário;
- caminho do backup, manifesto verificado e itens abrangidos;
- modo de gerenciamento do túnel: local/remoto;
- `ACCESS_APP: painelpix`, política, quantidade de regras e duração, sem e-mail;
- `PROTECT_WITH_ACCESS: ATIVO/NAO` e `JWT_VALIDATION_REQUIRED: SIM/NAO`;
- AUD correto configurado: `SIM/NAO`, sem token de sessão;
- rota/DNS criados: `SIM/NAO`, destino sanitizado e rollback disponível;
- teste não autenticado bloqueado: `SIM/NAO`;
- teste autorizado chegou ao `404` antes da ativação: `SIM/NAO`;
- `TURBORAMA_ADMIN_PUBLIC_HOST: CONFIGURADO/VAZIO`;
- login/logout TurboRama e logout Access testados: `SIM/NAO`;
- códigos HTTP sanitizados dos testes;
- hashes de estado/ambiente antes/depois e justificativa da única mudança prevista;
- clientes/licenças/máquinas `0/0/0`;
- preços alterados: `NAO`;
- `CREDENCIAL_MERCADOPAGO_ALTERADA: NAO`;
- `COBRANCA_CRIADA: NAO`;
- serviços, site e portas antes/depois;
- arquivos criados/alterados, sem conteúdo privado;
- rollback realizado ou não;
- último passo concluído e próximo passo exato no Windows.

Nunca incluir senha, hash PBKDF2, cookie, CSRF, JWT, cabeçalho Access, e-mail completo, token
Cloudflare, conteúdo do ambiente ou credencial financeira.

## Referências técnicas obrigatórias

- Cloudflare — criar a aplicação Access antes da rota e validar o token no origin:
  `https://developers.cloudflare.com/cloudflare-one/access-controls/applications/http-apps/self-hosted-public-app/`;
- Cloudflare — habilitar **Protect with Access** no cloudflared:
  `https://developers.cloudflare.com/tunnel/advanced/origin-parameters/#access`;
- Cloudflare — `originRequest.access.required=true`, com `teamName` e `audTag` específicos por
  hostname:
  `https://developers.cloudflare.com/api/resources/zero_trust/subresources/tunnels/subresources/cloudflared/subresources/configurations/`;
- TurboRama rodada 06: painel falha fechado quando `TURBORAMA_ADMIN_PUBLIC_HOST` está vazio e continua
  retornando `404` no hostname da API.
