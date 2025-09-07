using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OglesbyFDMembers.Data;

namespace OglesbyFDMembers.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("ProductVersion", "9.0.0");

            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.Person", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Active")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("TEXT");

                    b.Property<string>("Email")
                        .HasMaxLength(200)
                        .HasColumnType("TEXT");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("Phone")
                        .HasMaxLength(32)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("People", (string)null);
                });

            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.PersonAlias", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Alias")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("TEXT");

                    b.Property<int>("PersonId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("PersonId");

                    b.ToTable("PersonAliases", (string)null);
                });

            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.PersonAddress", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsPrimary")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsValidForMail")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Line1")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("TEXT");

                    b.Property<string>("Line2")
                        .HasMaxLength(200)
                        .HasColumnType("TEXT");

                    b.Property<string>("City")
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("State")
                        .HasMaxLength(64)
                        .HasColumnType("TEXT");

                    b.Property<string>("PostalCode")
                        .HasMaxLength(32)
                        .HasColumnType("TEXT");

                    b.Property<int>("PersonId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("PersonId");

                    b.HasIndex("PersonId", "IsPrimary", "IsValidForMail");

                    b.ToTable("PersonAddresses", (string)null);
                });

            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.Property", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Active")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("TEXT");

                    b.Property<string>("ParcelNumber")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("TEXT");

                    b.Property<string>("SitusAddress")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Properties", (string)null);
                });

            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.Ownership", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("TEXT");

                    b.Property<int>("PersonId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("PropertyId")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("PersonId");

                    b.HasIndex("PropertyId", "StartDate", "EndDate");

                    b.ToTable("Ownerships", (string)null);
                });

            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.Assessment", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<decimal>("AmountDue")
                        .HasColumnType("decimal(10,2)");

                    b.Property<decimal>("AmountPaid")
                        .HasColumnType("decimal(10,2)");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("TEXT");

                    b.Property<int>("PropertyId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Status")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Year")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("PropertyId");

                    b.HasIndex("PropertyId", "Year").IsUnique();

                    b.ToTable("Assessments", (string)null);
                });

            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.Payment", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<decimal>("Amount")
                        .HasColumnType("decimal(10,2)");

                    b.Property<string>("CheckNumber")
                        .HasMaxLength(50)
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsDonation")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Notes")
                        .HasMaxLength(500)
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("PaidUtc")
                        .HasColumnType("TEXT");

                    b.Property<int>("PaymentType")
                        .HasColumnType("INTEGER");

                    b.Property<int>("PersonId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("PersonId");

                    b.ToTable("Payments", (string)null);
                });

            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.PaymentAllocation", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<decimal>("Amount")
                        .HasColumnType("decimal(10,2)");

                    b.Property<int>("AssessmentId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("PaymentId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("AssessmentId");

                    b.HasIndex("PaymentId");

                    b.ToTable("PaymentAllocations", (string)null);
                });

            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.UtilityNotice", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<decimal>("Amount")
                        .HasColumnType("decimal(10,2)");

                    b.Property<DateTime>("ImportedUtc")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsAllocated")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("MatchedPersonId")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("NeedsReview")
                        .HasColumnType("INTEGER");

                    b.Property<string>("PayerNameRaw")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("MatchedPersonId", "IsAllocated");

                    b.ToTable("UtilityNotices", (string)null);
                });

            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.FeeSchedule", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<decimal>("AmountPerProperty")
                        .HasColumnType("decimal(10,2)");

                    b.Property<int>("Year")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("Year").IsUnique();

                    b.ToTable("FeeSchedules", (string)null);
                });

            // Relationships with Restrict delete behavior
            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.PersonAlias", b =>
                {
                    b.HasOne("OglesbyFDMembers.Domain.Entities.Person", "Person")
                        .WithMany("Aliases")
                        .HasForeignKey("PersonId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.PersonAddress", b =>
                {
                    b.HasOne("OglesbyFDMembers.Domain.Entities.Person", "Person")
                        .WithMany("Addresses")
                        .HasForeignKey("PersonId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.Ownership", b =>
                {
                    b.HasOne("OglesbyFDMembers.Domain.Entities.Person", "Person")
                        .WithMany("Ownerships")
                        .HasForeignKey("PersonId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("OglesbyFDMembers.Domain.Entities.Property", "Property")
                        .WithMany("Ownerships")
                        .HasForeignKey("PropertyId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.Assessment", b =>
                {
                    b.HasOne("OglesbyFDMembers.Domain.Entities.Property", "Property")
                        .WithMany("Assessments")
                        .HasForeignKey("PropertyId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.Payment", b =>
                {
                    b.HasOne("OglesbyFDMembers.Domain.Entities.Person", "Person")
                        .WithMany("Payments")
                        .HasForeignKey("PersonId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.PaymentAllocation", b =>
                {
                    b.HasOne("OglesbyFDMembers.Domain.Entities.Assessment", "Assessment")
                        .WithMany("PaymentAllocations")
                        .HasForeignKey("AssessmentId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("OglesbyFDMembers.Domain.Entities.Payment", "Payment")
                        .WithMany("Allocations")
                        .HasForeignKey("PaymentId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("OglesbyFDMembers.Domain.Entities.UtilityNotice", b =>
                {
                    b.HasOne("OglesbyFDMembers.Domain.Entities.Person", "MatchedPerson")
                        .WithMany("MatchedUtilityNotices")
                        .HasForeignKey("MatchedPersonId")
                        .OnDelete(DeleteBehavior.Restrict);
                });
        }
    }
}

