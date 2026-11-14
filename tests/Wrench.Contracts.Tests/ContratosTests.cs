using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Wrench.Contracts;
using Xunit;

namespace Wrench.Contracts.Tests;

public class ContratosTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static IEnumerable<Type> Mensagens => typeof(ISagaMensagem).Assembly
        .GetTypes()
        .Where(tipo => tipo.IsClass && !tipo.IsAbstract && typeof(ISagaMensagem).IsAssignableFrom(tipo));

    [Fact]
    public void Toda_mensagem_implementa_o_contrato_da_saga()
    {
        Mensagens.Should().HaveCountGreaterThan(10);
        Mensagens.Should().OnlyContain(tipo => tipo.IsSealed);
    }

    [Fact]
    public void Todo_tipo_declarado_corresponde_a_uma_mensagem()
    {
        var declarados = typeof(MensagemTipos)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(campo => campo.FieldType == typeof(string))
            .Select(campo => (string)campo.GetValue(null)!)
            .ToList();

        var existentes = Mensagens.Select(tipo => tipo.Name).ToList();

        declarados.Should().BeSubsetOf(existentes);
        existentes.Should().BeEquivalentTo(declarados);
    }

    [Fact]
    public void Comandos_e_eventos_do_orquestrador_nao_se_sobrepoem()
    {
        MensagemTipos.ComandosDoOrquestrador
            .Should().NotIntersectWith(MensagemTipos.EventosDoOrquestrador);

        MensagemTipos.ComandosDoOrquestrador
            .Concat(MensagemTipos.EventosDoOrquestrador)
            .Should().HaveCount(Mensagens.Count());
    }

    [Fact]
    public void Mensagem_serializa_e_desserializa_preservando_o_conteudo()
    {
        var original = new IniciarExecucao(
            Guid.NewGuid(),
            "corr-1",
            "TROCA DE EMBREAGEM",
            [new ItemExecucaoDto(Guid.NewGuid(), "Kit de embreagem", 1)],
            1250.90m);

        var json = JsonSerializer.Serialize(original, Json);
        var recuperada = JsonSerializer.Deserialize<IniciarExecucao>(json, Json);

        recuperada.Should().BeEquivalentTo(original);
        json.Should().Contain("ordemServicoId").And.Contain("correlationId");
    }

    [Theory]
    [InlineData(MensagemTipos.GerarOrcamento)]
    [InlineData(MensagemTipos.PagamentoAprovado)]
    [InlineData(MensagemTipos.ExecucaoFalhou)]
    public void Tipo_usa_o_nome_do_contrato(string tipo)
    {
        Mensagens.Select(mensagem => mensagem.Name).Should().Contain(tipo);
    }

    [Fact]
    public void Atributos_de_mensagem_sao_os_esperados_pelo_transporte()
    {
        AtributosMensagem.MessageId.Should().Be("messageId");
        AtributosMensagem.Tipo.Should().Be("tipo");
        AtributosMensagem.CorrelationId.Should().Be("correlationId");
        AtributosMensagem.Traceparent.Should().Be("traceparent");
    }
}
