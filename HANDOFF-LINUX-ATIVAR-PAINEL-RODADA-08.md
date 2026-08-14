# Handoff Linux — ativar a segunda barreira do painel — rodada 08

Data de preparação no Windows: 2026-08-08  
Ambiente de destino: servidor Linux em produção assistida  
Escopo exclusivo: ativar o login próprio do TurboRama no hostname administrativo já protegido

## Objetivo

Ativar o painel TurboRama somente em `painelpix.lzgames.com.br`, sem atualizar binários e sem
alterar o Cloudflare, o site, a API, o Mercado Pago, dados comerciais ou portas da empresa.

O resultado esperado possui duas barreiras independentes:

1. Cloudflare Access por código de uso único enviado ao e-mail exato autorizado;
2. login próprio do TurboRama, com usuário e hash PBKDF2 mantidos apenas no Linux.

## Estado confirmado antes desta rodada

- a rodada 06 está instalada no commit
  `102b8f0b6a436a999885188d0683a63d57755180`;
- SHA-256 esperado do DLL instalado:
  `014182be32bfa540a0cde9edbc988519728c79249db9402e9ee6cbf3076f92ee`;
- serviço TurboRama saudável em `127.0.0.1:5187`;
- `TURBORAMA_ADMIN_PUBLIC_HOST` ausente/vazio, portanto o painel falha fechado com `404`;
- clientes, licenças e máquinas: `0/0/0`;
- nenhuma tabela de preços, credencial Mercado Pago ou cobrança foi criada;
- o túnel remoto existente `lz-fix` está saudável e foi preservado;
- a rota publicada `painelpix.lzgames.com.br` já existe no túnel e aponta para
  `http://127.0.0.1:5187`;
- a validação Access/JWT está ativada nessa rota e associada ao aplicativo `painelpix`;
- a aplicação Access protege o hostname inteiro;
- a política anexada possui uma única regra `Allow` para um e-mail exato, sessão de 30 minutos;
- o método de autenticação efetivamente testado é código de uso único por e-mail;
- uma política reutilizável duplicada e não anexada existe no painel Cloudflare; não a alterar nem
  excluir nesta rodada;
- uma sessão não autorizada foi recusada pelo Cloudflare Access;
- o proprietário completou o código por e-mail e chegou ao `404` da origem. Isso comprova a rota,
  o Access e o comportamento fail-closed antes da ativação interna.

Nenhum e-mail, cookie, código, JWT, AUD, senha ou segredo deve aparecer no retorno.

## Proibições absolutas

- não alterar, recriar, converter, reiniciar ou reordenar o túnel `lz-fix`;
- não alterar DNS, aplicações Access, políticas, provedores, JWT ou rotas publicadas;
- não alterar nginx, site, MariaDB, firewall, NAT, roteador ou outras aplicações LZGames;
- não tocar nas portas `3302`, `3306`, `13306` e `23306`;
- não abrir a porta `5187` externamente; ela deve continuar somente em loopback;
- não mudar o hostname/API `pix.lzgames.com.br` nem colocar `/v1/*` atrás de login humano;
- não atualizar Git, pacote, DLL, JSONs de runtime ou unidade systemd, salvo rollback do arquivo de
  ambiente previsto neste documento;
- não criar cliente, licença, máquina, código de ativação, sessão, preços, order, QR ou pagamento;
- não solicitar, ler, testar, trocar ou exibir Access Token/Client Secret Mercado Pago;
- não registrar usuário administrativo, senha, hash completo, cookies, CSRF, códigos Access ou
  conteúdo de `server.env` em chat, comando, log ou retorno;
- não apagar backups, versões isoladas ou a política Cloudflare duplicada;
- não prosseguir diante de qualquer divergência do estado confirmado.

## Arquivos de referência obrigatórios

Ler integralmente antes de agir:

1. `RETORNO-LINUX-RODADA-06.md`;
2. `RETORNO-LINUX-RODADA-07.md`;
3. este handoff;
4. `README.md` e `deploy/linux/README.md` do commit instalado.

## Fase A — auditoria somente leitura

Registrar de forma sanitizada:

- commit do clone e SHA-256 do DLL instalado;
- estado ativo/habilitado de `turborama-pix`, `nginx`, `cloudflared` e `mariadb`;
- health local e público da API;
- bind da porta 5187 exclusivamente em loopback;
- listeners das portas protegidas, sem alterar nenhum deles;
- proprietário e modo de `/etc/turborama-pix/server.env`;
- apenas os **nomes** das variáveis `TURBORAMA_ADMIN_*`, nunca os valores;
- SHA-256 de `state.json`, `server.env`, unidade TurboRama e arquivos Cloudflare locais;
- site principal saudável;
- `pix.lzgames.com.br/admin`, `/admin/`, `/admin/login`,
  `/admin/assets/admin.css` e POST `/admin/actions/pix` retornando `404`;
- `pix.lzgames.com.br/v1/health` retornando `200`;
- acesso não autenticado a `painelpix.lzgames.com.br/admin` sendo interceptado pelo Access, sem
  alcançar o formulário TurboRama.

Não tentar auditar ou modificar o painel Cloudflare nesta rodada. A publicação e a prova autorizada
já foram realizadas no Windows.

### Parada obrigatória da Fase A

Parar sem alterações e criar o retorno como `BLOQUEADO_POR_DIVERGENCIA` se ocorrer qualquer item:

- commit/DLL diferente do esperado;
- algum serviço preexistente degradado;
- site, API ou health falhando;
- porta 5187 exposta fora do loopback;
- painel aparecendo no hostname da API;
- hostname administrativo contornando o Cloudflare Access;
- `server.env` fora de `root:root` e modo `0600`;
- estado comercial diferente de `0/0/0` ou qualquer indício de cobrança/credencial alterada;
- impossibilidade de obter senha administrativa por entrada local privada.

## Fase B — backup verificável anterior à única alteração

Criar uma pasta nova, exclusiva e `root:root 0700` em
`/var/backups/turborama-pix/round08-<UTC>` contendo cópias preservando metadados de:

- `/etc/turborama-pix/server.env`;
- unidade systemd efetiva do TurboRama;
- `/opt/turborama-pix`;
- `/var/lib/turborama-pix`;
- arquivos/unidade locais do cloudflared, apenas para prova e rollback; não editá-los.

Gerar `SHA256SUMS` sem expor conteúdo privado e verificar integralmente o manifesto antes de
prosseguir. Registrar somente caminho, permissões e resultado `OK`.

## Fase C — credencial administrativa privada

O usuário administrativo padrão pode ser `turborama-admin`. Se já existir um nome válido nas
variáveis privadas, preservá-lo.

Gerar o hash exclusivamente com o DLL instalado e o argumento interativo
`--hash-admin-password`. A senha deve ter entre 14 e 256 caracteres e ser digitada duas vezes pelo
proprietário em um terminal físico/privado, com eco desativado.

Regras obrigatórias:

- nunca pedir a senha pelo chat;
- nunca incluí-la em argumento, histórico, variável de shell, script, clipboard, arquivo temporário,
  log, retorno ou saída capturada;
- não mostrar nem copiar o hash para a conversa; transferi-lo diretamente para o arquivo privado;
- se não houver meio de entrada física/privada sem captura, parar como
  `BLOQUEADO_POR_ENTRADA_PRIVADA`, sem modificar o ambiente.

## Fase D — alteração mínima e atômica do ambiente

Editar somente `/etc/turborama-pix/server.env`, preservando byte a byte todas as demais variáveis e
sem duplicatas. O resultado deve conter exatamente uma ocorrência de cada nome:

```text
TURBORAMA_ADMIN_USERNAME=<nome válido preservado ou turborama-admin>
TURBORAMA_ADMIN_PASSWORD_HASH=<hash PBKDF2 gerado privadamente>
TURBORAMA_ADMIN_PUBLIC_HOST=painelpix.lzgames.com.br
TURBORAMA_ADMIN_KEY_DIRECTORY=/var/lib/turborama-pix/admin-data-protection
```

Não colocar aspas adicionais, URL, porta, caminho ou curinga no hostname. Fazer a substituição por
arquivo temporário privado no mesmo filesystem, validar nomes/duplicatas/permissões e promover
atomicamente. O arquivo final deve continuar `root:root 0600`.

O diretório de Data Protection deve permanecer sob `/var/lib/turborama-pix`, pertencer ao usuário
do serviço e ter modo `0700`. Não remover nem regenerar chaves que já existam.

Antes de reiniciar, validar de forma sanitizada:

- apenas os nomes das variáveis e a contagem `1` de cada variável administrativa;
- formato do usuário, hostname e hash usando a própria aplicação ou validação equivalente;
- nenhuma outra linha do ambiente mudou, exceto as quatro variáveis autorizadas;
- `state.json` permanece com o mesmo SHA-256.

## Fase E — ativação restrita e validações

Reiniciar **somente** `turborama-pix`. Não reiniciar nginx, cloudflared, MariaDB ou o site.

Validar, sem imprimir corpos, segredos ou cabeçalhos sensíveis:

1. serviço ativo e health local saudável;
2. health público da API saudável;
3. site principal saudável;
4. porta 5187 ainda somente em loopback;
5. hostname da API continua com `404` em todas as cinco rotas `/admin*` testadas;
6. hostname da API continua com `200` em `/v1/health`;
7. acesso anônimo ao hostname administrativo continua preso no Cloudflare Access;
8. depois de completar o código Access, `/admin` apresenta o login próprio do TurboRama, e não
   `404`;
9. uma senha incorreta retorna erro genérico e não revela detalhes;
10. a senha correta cria sessão administrativa com cookie `Secure`, `HttpOnly` e
    `SameSite=Strict`, sem registrar seu valor;
11. a página autenticada abre e mostra estado vazio `0/0/0`, sem criar ou alterar dados;
12. logout TurboRama invalida a sessão;
13. novo acesso exige novamente o login TurboRama; a expiração Access permanece 30 minutos;
14. logout/limpeza da sessão Access volta a exigir o código de e-mail.

Os itens 8–14 exigem confirmação visual do proprietário. Não pedir captura contendo código,
cookie, e-mail ou segredo. Se a confirmação visual não puder ser feita nesta rodada, manter o painel
ativado somente se todos os testes técnicos anteriores passarem e registrar
`ATIVADO_AGUARDANDO_TESTE_VISUAL`; não declarar conclusão comercial.

## Fase F — prova de preservação

Comparar antes/depois e registrar:

- SHA-256 de `state.json` idêntico;
- `server.env` diferente somente pelas quatro variáveis administrativas autorizadas;
- DLL, unidade systemd e arquivos Cloudflare com hashes idênticos;
- `turborama-pix`, nginx, cloudflared e MariaDB ativos/habilitados;
- site, API e health saudáveis;
- portas `3302`, `3306`, `13306`, `23306` inalteradas;
- 5187 somente em loopback;
- clientes/licenças/máquinas `0/0/0`;
- preços, Mercado Pago, cobrança, order, QR e pagamento inalterados.

## Rollback obrigatório

Executar rollback imediato se o serviço não iniciar, health falhar, o painel surgir no hostname da
API, o Access for contornado, cookies seguros não forem emitidos, o site for afetado ou qualquer
recurso fora do escopo mudar.

Rollback permitido:

1. restaurar atomicamente **somente** `server.env` da rodada 08;
2. restaurar proprietário/modo `root:root 0600`;
3. reiniciar somente `turborama-pix`;
4. provar painel desativado com `404` em ambos os hostnames de origem e API/health/site saudáveis;
5. preservar o backup, rota Cloudflare e todos os demais serviços;
6. registrar `ROLLBACK_EXECUTADO` e o motivo sanitizado.

Não remover rota/DNS/Access como parte deste rollback. A barreira externa já está correta e pode
continuar apontando para a origem fail-closed.

## Arquivo de retorno obrigatório

Criar `RETORNO-LINUX-RODADA-08.md` com:

- data/hora e resultado final entre:
  `ATIVADO_E_VALIDADO`, `ATIVADO_AGUARDANDO_TESTE_VISUAL`,
  `BLOQUEADO_POR_DIVERGENCIA`, `BLOQUEADO_POR_ENTRADA_PRIVADA` ou `ROLLBACK_EXECUTADO`;
- commit e SHA-256 do DLL instalado;
- caminho do backup e verificação do manifesto;
- hashes antes/depois, sem conteúdo privado;
- variáveis administrativas presentes apenas por nome, nunca valor;
- confirmação `CLOUDFLARE_ALTERADO: NAO`;
- códigos HTTP sanitizados dos testes;
- resultado do login/logout sem usuário, senha, e-mail, cookie, código ou hash;
- serviços, site e portas antes/depois;
- clientes/licenças/máquinas antes/depois;
- `PRECOS_ALTERADOS: NAO`;
- `CREDENCIAL_MERCADOPAGO_ALTERADA: NAO`;
- `COBRANCA_CRIADA: NAO`;
- arquivos alterados e rollback, se houver;
- último passo concluído e próximo passo exato.

Não fazer commit nem push no Linux. Retornar apenas esse arquivo para a conversa Windows.

## Critério de sucesso

Somente declarar `ATIVADO_E_VALIDADO` quando:

- o Cloudflare Access bloquear o visitante antes da origem;
- o código de e-mail autorizado liberar apenas o hostname administrativo;
- o login próprio do TurboRama funcionar e encerrar sessão corretamente;
- a API continuar invisível para `/admin*` e saudável em `/v1/health`;
- nenhum dado comercial/financeiro, serviço, site, porta ou configuração Cloudflare tiver mudado;
- backup e rollback estiverem comprovados.
