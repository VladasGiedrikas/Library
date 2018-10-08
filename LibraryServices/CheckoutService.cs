﻿using LibraryData;
using LibraryData.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LibraryServices
{
    public class CheckoutService : ICheckout
    {
        private LibraryDbContext _context;
        public CheckoutService(LibraryDbContext context)
        {
            _context = context;
        }

        public void Add(Checkout newCheckout)
        {
            _context.Add(newCheckout);
            _context.SaveChanges();
        }

       
        public IEnumerable<Checkout> GetAll()
        {
            return _context.Checkouts;
        }

        public Checkout GetById(int checkoutId)
        {
            return GetAll().FirstOrDefault(checkout => checkout.Id == checkoutId);
        }

        public IEnumerable<CheckoutHistory> GetCheckoutHistory(int id)
        {
            return _context.CheckoutHistories
                .Include(h => h.LibraryAsset)
                .Include(h => h.LibraryCard)
                .Where(h => h.LibraryAsset.Id == id);
        }   

        public IEnumerable<Hold> GetCurrentHolds(int id)
        {
            return _context.Holds
                 .Include(h => h.LibraryAsset)
                 .Where(h => h.LibraryAsset.Id == id);
        }

        public Checkout GetLatestCheckout(int assetId)
        {
            return _context.Checkouts
                .Where(c => c.LibraryAsset.Id == assetId)
                .OrderByDescending(c => c.Since)
                .FirstOrDefault();
        }

        public void MarkFound(int assetId)
        {
            var now = DateTime.Now;

            UpdateAssetStatus(assetId, "Available");

            RemoveExistingCheckouts(assetId);       
            
            CloseExistingCheckoutsHistory(assetId, now);

            _context.SaveChanges();

        }

        private void UpdateAssetStatus(int assetId, string newStatus)
        {
            var item = _context.LibraryAssets.FirstOrDefault(a => a.Id == assetId);
            _context.Update(item);

            item.Status = _context.Status.FirstOrDefault(status => status.Name == newStatus);
        }

        private void CloseExistingCheckoutsHistory(int assetId, DateTime now) //close any existing checkout history
        {
            var history = _context.CheckoutHistories.FirstOrDefault(h => h.LibraryAsset.Id == assetId && h.CheckedIn == null);

            if (history != null)
            {
                _context.Update(history);
                history.CheckedIn = now;
            }
            _context.SaveChanges();
        }

        private void RemoveExistingCheckouts(int assetId) //remove any eisting checkouts on the item
        {
            var checkout = _context.Checkouts.FirstOrDefault(co => co.LibraryAsset.Id == assetId);

            if (checkout != null)
            {
                _context.Remove(checkout);
            }
        }

        public void MarkLost(int assetId)
        {
            UpdateAssetStatus(assetId, "Lost");
            _context.SaveChanges();
        }

        public void CheckInItem(int id)
        {
            var now = DateTime.Now;
            var item = _context.LibraryAssets.FirstOrDefault(a => a.Id == id);

            // remove any existing checkouts on the item
            var checkout = _context.Checkouts
        .Include(c => c.LibraryAsset)
        .Include(c => c.LibraryCard)
        .FirstOrDefault(a => a.LibraryAsset.Id == id);
            if (checkout != null) _context.Remove(checkout);
            // close any existing checkout history
            var history = _context.CheckoutHistories
        .Include(h => h.LibraryAsset)
        .Include(h => h.LibraryCard)
        .FirstOrDefault(h =>
            h.LibraryAsset.Id == id
            && h.CheckedIn == null);
            if (history != null)
            {
                _context.Update(history);
                history.CheckedIn = now;
            }
            // look for existing holds on the item
            var currentHolds = _context.Holds
                .Include(h => h.LibraryAsset)
                .Include(h => h.LibraryCard)
                .Include(h => h.LibraryAsset.Id == id);
            //if there are holds, checkout the item to the librarycard with the earliest hold
            if (currentHolds.Any())
            {
                CheckoutToEarliestHold(id, currentHolds);
                return;
            }

            // otherwise, update the item status to available
            item.Status = _context.Status.FirstOrDefault(a => a.Name == "Available");
            _context.SaveChanges();
        }

        private void CheckoutToEarliestHold(int assetId, IIncludableQueryable<Hold, bool> currentHolds)
        {
            var earliestHold = currentHolds.OrderBy(holds => holds.HoldPlaced).FirstOrDefault();

            var card = earliestHold.LibraryCard;
            _context.Remove(earliestHold);
            _context.SaveChanges();
            CheckOutItem(assetId, card.Id);
        }

        public void CheckOutItem(int assetId, int libraryCardId)
        {
            if (IsCheckedOut(assetId))
            {
                return;
                //Add logic to handle feedback to the user
            }
            var item = _context.LibraryAssets.FirstOrDefault(a => a.Id == assetId);

            UpdateAssetStatus(assetId, "Checked Out");

            var libraryCard = _context.LibraryCards
                .Include(card => card.Checkouts)
                .FirstOrDefault(card => card.Id == libraryCardId);

            var now = DateTime.Now;
            var checkout = new Checkout
            {
                LibraryAsset = item,
                LibraryCard = libraryCard,
                Since = now,
                Until = GetDefaultCheckoutTime(now)
            };
            _context.Add(checkout);

            var checkoutHistory = new CheckoutHistory
            {
                CheckedOut = now,
                LibraryAsset = item,
                LibraryCard = libraryCard
            };
            _context.Add(checkoutHistory);
            _context.SaveChanges();

        }

        private DateTime GetDefaultCheckoutTime(DateTime now)
        {
            return now.AddDays(30);
        }

        public bool IsCheckedOut(int assetId)
        {
            return _context.Checkouts.Where(co => co.LibraryAsset.Id == assetId).Any();
        }

        public void PlaceHold(int assetId, int libraryCardId)
        {
            var now = DateTime.Now;

            var asset = _context.LibraryAssets.Include(a => a.Status).FirstOrDefault(a => a.Id == assetId);

            var card = _context.LibraryCards.FirstOrDefault(c => c.Id == libraryCardId);

            if (asset.Status.Name == "Available")
            {
                UpdateAssetStatus(assetId, "On Hold");
            }

            var hold = new Hold
            {
                HoldPlaced = now,
                LibraryAsset = asset,
                LibraryCard = card
            };

            _context.Add(hold);
            _context.SaveChanges();
        }

        public string GetCurrentHoldPatronName(int holdId)
        {
            var hold = _context.Holds
                 .Include(h => h.LibraryAsset)
                 .Include(h => h.LibraryCard)
                 .FirstOrDefault(h => h.Id == holdId);

            var cardId = hold?.LibraryCard.Id; //? if hold is null

            var patron = _context.Patrons.Include(p => p.LibraryCard).FirstOrDefault(p => p.LibraryCard.Id == cardId);

            return patron?.FirstName + " " + patron?.LastName;
        }

        public DateTime GetCurrentHoldPlaced(int holdId)
        {
            return
                   _context.Holds
                  .Include(h => h.LibraryAsset)
                  .Include(h => h.LibraryCard)
                  .FirstOrDefault(h => h.Id == holdId)
                  .HoldPlaced;

        }

        public string GetCurrentCheckoutPatron(int assetId)
        {
            var checkout = GetCheckoutByAssetId(assetId);
            if (checkout == null)
            {
                return "";
            }
            var cardId = checkout.LibraryCard.Id;

            var patron = _context.Patrons.Include(p => p.LibraryCard).FirstOrDefault(p => p.LibraryCard.Id == cardId);

            return patron.FirstName + " " + patron.LastName;
        }

        private Checkout GetCheckoutByAssetId(int assetId)
        {
            return _context.Checkouts
                 .Include(co => co.LibraryAsset)
                 .Include(co => co.LibraryCard)
                 .FirstOrDefault(co => co.LibraryAsset.Id == assetId);               
        }     
    }
}