using ControleFinanceiro.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFinanceiro.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Transacao> Transacoes => Set<Transacao>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cartao> Cartoes => Set<Cartao>();
    public DbSet<Conta> Contas => Set<Conta>();
    public DbSet<Transferencia> Transferencias => Set<Transferencia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transacao>()
            .HasOne(t => t.Categoria)
            .WithMany()
            .HasForeignKey(t => t.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Dois usuários não podem ter o mesmo e-mail.
        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Categoria>()
            .HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Transacao>()
            .HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(t => t.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // Não deixa apagar um cartão que tem compras.
        modelBuilder.Entity<Transacao>()
            .HasOne(t => t.Cartao)
            .WithMany()
            .HasForeignKey(t => t.CartaoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Cartao>()
            .HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // Contas: não deixa apagar uma conta que tem transações, cartões ou transferências.
        modelBuilder.Entity<Conta>()
            .HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Transacao>()
            .HasOne(t => t.Conta)
            .WithMany()
            .HasForeignKey(t => t.ContaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Cartao>()
            .HasOne<Conta>()
            .WithMany()
            .HasForeignKey(c => c.ContaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Transferencia>()
            .HasOne<Conta>()
            .WithMany()
            .HasForeignKey(t => t.ContaOrigemId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Transferencia>()
            .HasOne<Conta>()
            .WithMany()
            .HasForeignKey(t => t.ContaDestinoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Transferencia>()
            .HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(t => t.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
