# Retorno do servidor Linux — rodada 02

Data/hora: 2026-08-07T08:58:58-03:00  
Status da rodada: `BLOQUEADO_POR_FIREWALL`

## Motivo da parada

A auditoria somente leitura confirmou uma condição de parada definida no handoff:

- MariaDB escuta em todas as interfaces IPv4 na porta TCP 3306;
- o UFW permite entrada na porta 3306 a partir de qualquer origem em IPv4 e IPv6;
- existem regras globais redundantes para 3306 e 13306;
- existe também uma regra restrita a uma origem específica, omitida deste retorno;
- a política padrão do UFW é negar entrada, mas as permissões globais de 3306 anulam essa proteção
  para a porta do MariaDB.

Por segurança, a rodada foi interrompida antes de cadastrar credencial financeira, criar dados de
produção, parar serviços ou alterar estado.

## Auditoria sanitizada do firewall

- gerenciador ativo: UFW;
- logging: ativo, nível baixo;
- política padrão de entrada: negar;
- política padrão de saída: permitir;
- política padrão de encaminhamento: negar;
- 139/TCP: processo escutando, sem regra explícita de permissão; sujeito à política de bloqueio;
- 445/TCP: processo escutando, sem regra explícita de permissão; sujeito à política de bloqueio;
- 3306/TCP: processo escutando em todas as interfaces e permitido globalmente em IPv4 e IPv6;
- 8083/TCP: processo escutando, sem regra explícita de permissão; sujeito à política de bloqueio;
- 13306/TCP/UDP: permitido globalmente, mas sem processo TCP escutando no momento da auditoria;
- regras efetivas de iptables/ip6tables confirmam política INPUT DROP e permissões globais citadas.

Nenhum endereço IP público ou privado completo foi incluído neste retorno.

## Serviços e endpoints confirmados

| Item | Estado |
|---|---|
| `turborama-pix` | ativo e habilitado |
| `cloudflared` | ativo e habilitado |
| `nginx` | ativo e habilitado |
| endpoint local `/v1/health` na porta 5187 | saudável, `ready: true` |
| `https://pix.lzgames.com.br/v1/health` | saudável, `ready: true` |

O TurboRama PIX permanece limitado à interface de loopback na porta 5187.

## Backup novo desta rodada

- backup criado: NÃO;
- caminho: não aplicável;
- manifesto SHA-256: não aplicável;
- rollback novo: não aplicável.

Motivo: o critério de parada por firewall ocorreu antes da fase de backup e de qualquer alteração de
estado. Os backups preexistentes não foram modificados.

## Dados de produção

- Cliente planejado: `CLI-TURBORAMA-TESTE`;
- Licença planejada: `TR-TURBORAMA-TESTE-001`;
- Perfil planejado: `SOFTWARE_BOUND_ONLINE`;
- Máquinas permitidas: `1`;
- identificadores existentes inspecionados: NÃO, devido à parada antecipada;
- preços de 15, 30, 45, 60 e 120 minutos: NÃO INFORMADOS / NÃO CADASTRADOS;
- PDV Mercado Pago: NÃO INFORMADO / NÃO CADASTRADO;
- `CREDENCIAL_CONFIGURADA: NAO`;
- `CODIGO_ATIVACAO_GERADO: NAO`;
- gabinete ativado: NÃO;
- cobrança, order ou QR criado: NÃO.

Nenhum segredo foi solicitado, lido ou exibido.

## Estado final

- `turborama-pix`: ativo;
- `cloudflared`: ativo;
- `nginx`: ativo;
- endpoint local: saudável;
- endpoint público: saudável;
- firewall: não alterado;
- nginx e Cloudflare: não alterados;
- dados do TurboRama: não alterados.

## Comandos executados

Todos os comandos foram somente leitura e não continham valores secretos:

```bash
ss -lnt
systemctl is-active turborama-pix cloudflared nginx
systemctl is-enabled turborama-pix cloudflared nginx
curl --fail --silent --show-error --max-time 10 http://LOOPBACK:5187/v1/health
curl --fail --silent --show-error --max-time 15 https://pix.lzgames.com.br/v1/health
pkexec /usr/sbin/ufw status verbose
pkexec /usr/sbin/iptables -S
pkexec /usr/sbin/ip6tables -S
date --iso-8601=seconds
```

## Arquivos criados ou alterados

- criado:
  `/home/lz-servidor/turborama-download/Servidor-pix/RETORNO-LINUX-RODADA-02.md`;
- nenhum arquivo de configuração foi alterado;
- nenhum script administrativo foi criado;
- nenhum commit ou push foi realizado.

## Erros e avisos

- aviso crítico: MariaDB está permitido globalmente pelo firewall em IPv4 e IPv6;
- aviso: regras redundantes liberam 3306 e 13306;
- nenhuma falha foi observada nos três serviços ou nos endpoints PIX;
- nenhuma tentativa de correção foi feita porque o handoff proíbe alteração do firewall sem confirmação
  explícita e determina parada antes da credencial financeira.

## Último passo concluído

Auditoria sanitizada das portas e do firewall, com confirmação de saúde do TurboRama PIX, nginx,
Cloudflare Tunnel e endpoints local/público.

## Próximo passo exato no Windows

1. anexar este arquivo à conversa do Windows;
2. revisar a exposição do MariaDB e identificar todos os clientes externos legítimos antes de remover
   qualquer regra;
3. preparar uma rodada Linux específica para backup e restrição controlada das regras globais de 3306
   e 13306, preservando somente acessos necessários;
4. após corrigir e validar o firewall, emitir novo handoff para retomar a rodada 02 a partir da fase B;
5. somente então informar os cinco preços e o identificador não secreto do PDV, mantendo o Access Token
   exclusivamente na entrada oculta do terminal Linux.
