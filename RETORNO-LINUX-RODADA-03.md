# Retorno Linux — auditoria de portas da empresa — rodada 03

Data/hora: 2026-08-07T09:22:30-03:00  
Resultado: `AUDITORIA_SOMENTE_LEITURA_CONCLUIDA`

## Garantia de não alteração

Nenhuma regra de UFW, iptables, ip6tables, nftables, NAT ou roteamento foi alterada. Nenhum serviço foi
parado, reiniciado, recarregado ou reconfigurado. MariaDB, nginx, Cloudflare Tunnel, TurboRama PIX,
arquivos de aplicação e dados permaneceram inalterados. Nenhuma credencial, licença, preço, PDV,
ativação ou cobrança foi criada.

## Estado final

| Item | Estado final |
|---|---|
| `turborama-pix` | ativo e habilitado |
| `cloudflared` | ativo e habilitado |
| `nginx` | ativo e habilitado |
| `mariadb` | ativo e habilitado |
| health local PIX, loopback porta 5187 | saudável, `ready: true` |
| `https://pix.lzgames.com.br/v1/health` | saudável, `ready: true` |

## Mapa das portas

| Porta | Listener/processo | Serviço | Vínculo | Finalidade e evidência sanitizada |
|---:|---|---|---|---|
| 3302 | nenhum listener encontrado | nenhum serviço local identificado | nenhum vínculo TCP/UDP local | Porta declarada como pertencente ao sistema da empresa. A dependência pode estar fora deste host ou em equipamento de borda; não foi tecnicamente observável no servidor. Deve permanecer intocada. |
| 3306 | `mariadbd`, PID observado 2195 | `mariadb.service` | TCP, todas as interfaces IPv4; sem listener IPv6 observado | Banco das aplicações LZGames. Uso local confirmado e referências em aplicações PHP/Node. Há também evidência de tráfego de origem `INTERNET`. |
| 13306 | nenhum listener encontrado | nenhum serviço local identificado | nenhum vínculo TCP/UDP local | Não foi encontrado proxy, NAT, redirecionamento, unidade systemd, referência de configuração ou tráfego contado. Finalidade atual não confirmada. |

O PID é apenas o observado nesta execução e pode mudar após reinicialização futura.

## MariaDB

- porta efetiva: 3306;
- `bind-address` efetivo: todas as interfaces IPv4;
- unidade: `/usr/lib/systemd/system/mariadb.service`;
- drop-in:
  `/etc/systemd/system/mariadb.service.d/migrated-from-my.cnf-settings.conf`;
- processo executado: `/usr/sbin/mariadbd`;
- usuário/grupo: `mysql:mysql`;
- conexões atuais durante a coleta: somente categoria `LOOPBACK`;
- nenhum conteúdo de tabela foi consultado.

## Firewall e redirecionamentos

O gerenciador efetivo é UFW sobre o backend iptables-nft:

- política padrão INPUT: DROP;
- política padrão FORWARD: DROP;
- política padrão OUTPUT: ACCEPT;
- porta 3302: nenhuma regra específica encontrada;
- porta 3306: uma permissão TCP para origem específica e permissões globais redundantes em IPv4;
- porta 3306: permissões globais TCP e UDP redundantes em IPv6;
- porta 13306: permissões globais TCP e UDP redundantes em IPv4 e IPv6;
- NAT IPv4/IPv6: nenhum DNAT, REDIRECT ou regra relativa a 3302, 3306 ou 13306;
- nftables confirmou tráfego na permissão específica de 3306 e também na permissão TCP global;
- contadores das regras 13306 estavam zerados;
- nenhuma regra ou contador indicou uso da porta 3302 neste host.

## Categorias de origem observadas

### Porta 3302

- conexões atuais: nenhuma;
- evidência recente local: nenhuma;
- categorias observadas: nenhuma;
- observação: a propriedade empresarial é informação confirmada pelo proprietário, mas a dependência
  técnica não está instalada ou visível neste servidor.

### Porta 3306

- conexões atuais: `LOOPBACK`;
- registros MariaDB dos últimos 30 dias, classificados sem endereços:
  - `LOOPBACK`: 88 ocorrências;
  - `INTERNET`: 203 ocorrências;
- a permissão específica para cliente externo registrava 194 pacotes;
- a primeira permissão TCP global registrava 86 pacotes;
- não foi possível provar que todo tráfego da regra global pertence a cliente legítimo;
- nenhuma categoria `VPN/TUNEL` foi identificada;
- nenhuma categoria `REDE_LOCAL` foi identificada nos registros analisados.

As ocorrências do log são principalmente conexões abortadas/avisos e não provam, isoladamente, uso
comercial legítimo. A regra específica com tráfego indica um acesso externo deliberadamente
permitido, mas sua necessidade deve ser confirmada com o proprietário.

### Porta 13306

- conexões atuais: nenhuma;
- listener: nenhum;
- contadores das permissões: zero;
- referências de configuração: nenhuma;
- categorias observadas: nenhuma.

## Referências de configuração e propósito

Referências relevantes, sem conteúdo integral:

- `/etc/mysql/mariadb.cnf`: configuração principal/inclusões do MariaDB;
- `/etc/mysql/mariadb.conf.d/99-remote.cnf`: configuração de acesso remoto/bind do MariaDB;
- `/etc/systemd/system/mariadb.service.d/migrated-from-my.cnf-settings.conf`: drop-in do serviço;
- `/home/lz-servidor/Documentos/lzgames/api/db.js`: conexão da API ao banco;
- `/home/lz-servidor/Documentos/lzgames/api/dbCashback.js`: conexão da API de cashback;
- `/home/lz-servidor/Documentos/lzgames/lzdb-bridge/server.js`: bridge de banco;
- aplicações PHP sob `agenda/`, `chatbot/`, `juridico/`, `suporte/` e `systema/`: clientes
  locais do MariaDB.

Arquivos de dependências, fontes, backups e assets contendo números coincidentes foram descartados como
falsos positivos. Nenhuma referência operacional válida a 3302 ou 13306 foi encontrada.

## Conclusões por porta

### 3302

A porta é uma dependência empresarial declarada e deve permanecer preservada. Não há listener, regra
local, conexão, unidade ou referência operacional observável neste host. Sua implementação pode estar
no roteador, em outra máquina ou temporariamente inativa. Classificação técnica: `NÃO VERIFICADO
NESTE HOST`.

### 3306

É necessária para clientes `LOOPBACK` das aplicações LZGames. Existe também acesso `INTERNET`
intencional sugerido pela regra específica com tráfego. A necessidade da permissão específica deve ser
confirmada. As permissões globais não são justificadas pelas evidências e ampliam desnecessariamente a
exposição.

### 13306

Não foi demonstrada necessidade atual. Não existe listener, NAT, proxy, referência operacional,
conexão ou contador de uso. As permissões globais parecem redundantes, mas nenhuma remoção deve ocorrer
antes de confirmação do proprietário.

## Permissões aparentemente redundantes

- duplicatas globais TCP 3306;
- permissões UDP 3306, pois o MariaDB observado usa TCP;
- permissões globais IPv6 3306 sem listener IPv6 observado;
- todas as permissões globais TCP/UDP 13306, condicionadas à confirmação de que não existe dependência
  externa temporariamente inativa;
- a permissão específica de 3306 não deve ser removida antes de identificar e validar o cliente.

Nenhuma proposta envolve a porta 3302.

## Plano reversível proposto — não executado

1. confirmar com o proprietário o cliente externo autorizado de 3306 e a finalidade histórica de
   13306;
2. confirmar em equipamento de borda se 3302 ou 13306 possuem encaminhamento fora deste servidor;
3. criar backup root-only de `/etc/ufw` e capturas verificáveis de UFW, iptables, ip6tables e nftables;
4. registrar testes atuais de API, sites, banco, SSH, Cloudflare e PIX;
5. preservar integralmente tudo relacionado a 3302;
6. manter inicialmente a permissão específica necessária de 3306;
7. remover em etapas somente duplicatas e permissões globais comprovadamente desnecessárias;
8. validar após cada remoção, mantendo comandos de restauração exatos e uma sessão administrativa
   aberta;
9. observar logs e clientes por uma janela definida antes de considerar alteração do
   `bind-address`;
10. tratar mudança do bind do MariaDB como etapa separada, somente depois de estabilizar o firewall.

## Pontos ainda desconhecidos

- onde a porta empresarial 3302 é implementada fora deste host;
- identidade funcional do cliente externo específico autorizado em 3306;
- se existe cliente legítimo ocasional que dependa da regra global 3306;
- finalidade histórica de 13306;
- existência de redirecionamentos no roteador ou firewall externo, que não são visíveis no servidor.

## Comandos somente leitura executados

```bash
ss -lntup '( portas 3302, 3306 e 13306 )'
ss -ntup '( conexões das portas 3302, 3306 e 13306 )'
ufw status numbered
iptables -S
ip6tables -S
iptables -t nat -S
ip6tables -t nat -S
nft list ruleset
systemctl status mariadb
systemctl show mariadb
mariadb -NBe 'consulta somente de variáveis, processo e categorias sanitizadas'
rg -l '3302|3306|13306' nos diretórios de configuração e aplicações
journalctl -u mariadb --since '-30 days' com classificação sanitizada
systemctl is-active/is-enabled turborama-pix cloudflared nginx mariadb
curl dos endpoints de saúde local e público
date --iso-8601=seconds
```

Nenhum comando continha credencial. Endereços completos, conteúdo de banco, arquivos `.env`,
segredos e valores privados foram omitidos.
