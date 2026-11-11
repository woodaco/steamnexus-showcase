using BDD_Tests.PageObjects;
using Microsoft.Extensions.Configuration;
using System;
using BDD_Tests.Drivers;
using TechTalk.SpecFlow;

using NUnit.Framework;

namespace BDD_Tests.StepDefinitions
{
    using TechTalk.SpecFlow;
    using FluentAssertions;
    using BDD_Tests.PageObjects;
    using OpenQA.Selenium;

    [Binding]
    [Parallelizable(ParallelScope.None)]
    public class KODO137StepDefinitions
    {
        private readonly DashboardPageObject _dashboardPage;

        public KODO137StepDefinitions(ScenarioContext context, BrowserDriver browserDriver)
        {
            _dashboardPage = new DashboardPageObject(browserDriver.Current);
        }

        [When(@"I view my dashboard")]
        public void WhenIViewMyDashboard()
        {
            // Assume there's a method in the DashboardPageObject to navigate to the dashboard
            _dashboardPage.GoTo("Dashboard");
        }

       

        [Then(@"I should see a badge named ""(.*)""")]
        public void ThenIShouldSeeABadgeNamed(string badgeName)
        { var badge = _dashboardPage.GetBadgeByName(badgeName); 
            badge.Should().NotBeNull();
        }
        
        [When(@"I click on the badge named ""(.*)""")]
        public void WhenIClickOnTheBadgeNamed(string badgeName)
        {
            _dashboardPage.GoToBadgeDetails(badgeName);
        }

        [Then(@"I should see a detailed description of the badge ""(.*)""")]
        public void ThenIShouldSeeADetailedDescriptionOfTheBadge(string badgeName)
        {
            // After clicking the badge, check if a description is visible
            // The description is shown via Bootstrap collapse, so we check for visible description elements
            _dashboardPage.IsBadgeDescriptionVisible().Should().BeTrue();

            // Also verify the description has some text content
            var visibleDescription = _dashboardPage.GetVisibleBadgeDescription();
            visibleDescription.Should().NotBeNull();
            visibleDescription.Text.Should().NotBeNullOrEmpty();
        }
    }

}
