using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Joonasw.ElectronicSigningDemo.Data
{
    public class Signer
    {
        public int Id { get; set; }
        public string Email { get; set; }
        /// <summary>
        /// Identifies the orchestration instance waiting for this
        /// signer's signature.
        /// Used to send a signing event.
        /// </summary>
        public string WaitForSignatureInstanceId { get; set; }
        public bool Signed { get; set; }
        public DateTimeOffset? DecidedAt { get; set; }

        [ForeignKey(nameof(Request))]
        public Guid RequestId { get; set; }
        public SigningRequest Request { get; set; }
    }
}