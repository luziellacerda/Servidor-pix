> Sucessora publicada às15h45Maceió: [Cadastro de clientes e liberação Station](RETORNO-CADASTRO-CLIENTES-STATION-20261006.md). A nova página cadastra cliente novo/existente e cria licença/código. A orientação abaixo de procurar somente licenças/Vendas e seus PIDs/rollback é histórica; não aplicar esse rollback sobre a sucessora.

# Códigos do Turborama Station disponíveis no painel — 06/10/2026

## Produção conferida

Publicado em **06/10/2026 às 14:04:50 (Maceió)**; HTTPS autenticado conferido às **14:07:15**. Fonte da interface: `17e564a23b275659707b33c2100d40487151fbde`.

Página: [Códigos Station](https://turbobox.lzgames.com.br/admin/station). Login: [Administração TurboBox](https://turbobox.lzgames.com.br/admin/login). Use a conta administrativa do site.

O recurso de emissão já existia dentro de **Abrir atendimento**. O menu se chamava apenas **Station**, a página enfatizava licenças/aparelhos e a tabela não oferecia um botão de emissão. A publicação torna o recurso visível e explica o uso no aplicativo.

## Caminho para o operador

1. Abra **Códigos Station** no menu. Na visão geral também há **Gerar código do app Station**.
2. Use **Gerar código de acesso** no cabeçalho para chegar à busca do cliente.
3. Busque o nome, a licença ou o pedido.
4. Clique em **Gerar código** para uma licença aguardando ativação. Se já existe um aparelho vinculado, use **Trocar celular** para gerar o código do novo aparelho.
5. Confira o cliente e o efeito, mantenha ou ajuste o motivo e confirme com **sua senha administrativa do site**.
6. Copie o código para a tela de ativação do app Turborama Station. Ele vale **30 minutos**, é de uso único e fica visível somente até fechar ou atualizar a página.

A senha administrativa confirma a operação no site. O cliente usa o código gerado no aplicativo. O código anterior deixa de funcionar após a emissão de outro. Não há recuperação do texto de um código antigo; gere outro quando necessário.

**Um aparelho ativo por licença.** A troca encerra a autorização do anterior. Para dois celulares ao mesmo tempo, cada um precisa de sua própria licença. Em produção, as duas licenças consultadas já estavam ativadas: ambas oferecem **Trocar celular**, e nenhuma oferece emissão comum para primeiro acesso. A conferência não alterou seus vínculos ou códigos.

Se o cliente não estiver na lista, confira a compra do Station em **Vendas** e a confirmação em **Pagamentos**. A licença é criada pelo fluxo comercial. Emissão e troca usam a licença existente; esta página não cadastra uma venda adicional nem libera pagamento pendente.

## Outros atendimentos

**Abrir atendimento** continua disponível na mesma linha do cliente:

| Situação | Opção |
| --- | --- |
| Reinstalação no mesmo celular | Cliente reinstalou o aplicativo |
| Outro celular | Trocar de celular |
| Remover vínculo e emitir depois | Liberar outra ativação |
| Celular perdido/roubado | Bloquear acesso |
| Bloqueio resolvido | Desbloquear acesso |
| Código entregue errado | Cancelar código emitido |
| Renovar sessão usando a chave salva no app | Reconectar aplicativo |

O painel oferece ações conforme o estado atual e mostra o efeito antes da confirmação. WhatsApp permanece opcional, desmarcado inicialmente. A fila só é solicitada quando o operador marca a opção e confirma a emissão.

## Implementação publicada

- Quatro arquivos Station atualizados: página PHP, política de rótulos, JavaScript e CSS.
- Novo `station-access-link.css`, com estilos restritos ao atalho Station na visão geral.
- Quatro templates receberam substituições exatas de navegação: `panel.php`, `admin-product-edit.php`, `admin-customer.php` e `admin-payments.php`. Em `panel.php`, o atalho e seu CSS ficam dentro da área administrativa.
- Cache dos assets novos: `v=20261006-1`; três assets públicos responderam HTTP200 com SHA256 idêntico à fonte.
- O atalho do cabeçalho apenas leva à busca. Os botões da tabela consultam o atendimento atualizado antes de abrir a confirmação. A operação continua pelo POST administrativo existente, com senha, CSRF, motivo, gerações esperadas e recibo idempotente.
- Erro de consulta, estado divergente ou ação indisponível não dispara uma emissão. Cancelar a confirmação mantém o aparelho autorizado.

Manifesto dos nove destinos e hashes: `ops/station-admin/codes-panel-20261006.json`. [Fonte do publicador e retorno](https://github.com/luziellacerda/Servidor-pix/blob/17e564a23b275659707b33c2100d40487151fbde/ops/station-admin/deploy-codes-panel.py). [Guia de operação](https://github.com/luziellacerda/Servidor-pix/blob/17e564a23b275659707b33c2100d40487151fbde/ops/station-admin/README.md).

## Verificações

- PHP e JavaScript passaram nas verificações de sintaxe.
- PostgreSQL temporário com roles reais: emissão30min, legado48h, replay, conflito de geração, cancelamento, transferência, bloqueio/desbloqueio, reconexão, vínculo e sessão anteriores recusados, histórico sem código.
- Chrome com dados sintéticos: página Station em1440/390px, guia, busca, botão de emissão, consulta atualizada, senha incorreta recusada, CSRF recusado, código exibido/ocultado, nenhum armazenamento local/sessionStorage e WhatsApp opt-in.
- Licença sintética vinculada: **Trocar celular** abre a confirmação correta, exige senha e mostra que o anterior perde acesso. Cancelar preservou ambas as gerações e o vínculo `BOUND`.
- Atalho da visão geral conferido como link, botão com área mínima44px e navegação para Station. Esse teste cobre o atalho; não homologa o restante das páginas do TurboBox.
- Publicador/retorno exercitados em cópia isolada: nove arquivos, originais restaurados, CSS novo removido e edição posterior recusada antes de restaurar.
- Produção: nove hashes e backups originais conferidos; `/admin` e `/admin/station` autenticados HTTP200; novas opções visíveis; assets HTTPS idênticos à fonte; sessão de conferência encerrada.

Detalhes sanitizados em `PAINEL-CODIGOS-STATION-PRODUCAO-20261006.json`. Capturas e logs sintéticos ficam privados em `/mnt/DADOS/station-codes-panel-check-20261006/browser`.

## Operação e retorno

Publicação apenas de arquivos, sem migration, reinício de serviço ou emissão em licença real. API Station PID875574, gestão PID140743 e helper PID140751 mantiveram seus estados/ExecStart. PIX, API Suite, administração Suite e gateway também conservaram seus PIDs/ExecStart. Foram enviados zero pedidos de alteração de licença e zero mensagens WhatsApp em produção.

Backup privado0700: `/home/lz-servidor/station-codes-panel-backup-20261006-m6gt2la9`; originais e `state.json`0600. Retorno específico desta interface:

```bash
python3 ops/station-admin/deploy-codes-panel.py --rollback \
  /home/lz-servidor/station-codes-panel-backup-20261006-m6gt2la9
```

O retorno confere todos os hashes, restaura apenas os arquivos deste ajuste e remove seu CSS novo. Recusa sobrepor edição posterior. Não execute o publicador histórico `deploy.py` de03/10 para atualizar esta interface.

## Comparação com o aplicativo e o servidor

A mudança está no site. O aplicativo continua usando o contrato de ativação existente; **não exige recompilar o APK**.

API em produção: fonte `a2bb176530fd4d2dfa740da7e934fd84d097404e`, DLL `d181bf97d5b39a334e95144267d6ece3f11d4e659a314d7d16cd2746e1999e13`, comunidade R41 habilitada. Catálogo14/2212 e relay512/1024 seguem o retorno R41.

O recibo Android `5e40f7e0f09942aeb9c9f8caf57e8a3697973495` registra a R41 instalada no Samsung, APK `b6b19321ec529945870dc919c3de5f0eba75aa54330d3672227b8e632302b28d`. Fontes, APK, DEX, runtime, licença, jogos e saves não foram alterados por esta publicação. POCO, gameplay em dupla, Pessoas/Voltar/correspondência de títulos e latência externa continuam com as pendências do [retorno da comunidade R41](RETORNO-COMUNIDADE-STATION-R41-20261006.md).
