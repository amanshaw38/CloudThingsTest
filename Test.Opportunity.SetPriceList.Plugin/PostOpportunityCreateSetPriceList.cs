using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Test.Plugins;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Test.Plugins
{
    public class PostOpportunityCreateSetPriceList : IPlugin
    {
        #region Constants

        #endregion

        #region IPlugin.ExecuteMethod
        /// <summary>
        /// Set schema name of the entity and messages this plugin is expected to registered for.
        /// </summary>
        /// <param name="serviceProvider"></param>
        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException("serviceProvider");
            }

            var localcontext = new LocalPluginContext(serviceProvider);

            localcontext.Trace(string.Format(CultureInfo.InvariantCulture, "Entered {0}.Execute()", this.ChildClassName));

            try
            {
                // Iterate over all of the expected registered events to ensure that the plugin
                // has been invoked by an expected event
                // For any given plug-in event at an instance in time, we would expect at most 1 result to match.
                Action<LocalPluginContext> entityAction =
                    (from result in this.RegisteredEvents
                     where (
                            result.Item1 == localcontext.PluginExecutionContext.Stage &&
                            result.Item2 == localcontext.PluginExecutionContext.MessageName &&
                            (string.IsNullOrWhiteSpace(result.Item3) ? true : result.Item3 == localcontext.PluginExecutionContext.PrimaryEntityName)
                    )
                     select result.Item4).FirstOrDefault();

                if (entityAction != null)
                {
                    localcontext.Trace(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} is firing for Entity: {1}, Message: {2}",
                        this.ChildClassName,
                        localcontext.PluginExecutionContext.PrimaryEntityName,
                        localcontext.PluginExecutionContext.MessageName));
                    entityAction.Invoke(localcontext);
                    // now exit - if the derived plug-in has incorrectly registered overlapping event registrations,
                    // guard against multiple executions.
                    return;
                }

                #region OptionalEntityAndInvokingMessageValidation

                //Ensure that this plugin is registered against Update Event of contact entity. If not report an exception
                if (localcontext.PluginExecutionContext.PrimaryEntityName != Opportunity.EntityLogicalName
                        || !(new[] { "CREATE" }).Contains(localcontext.PluginExecutionContext.MessageName.ToUpper()))
                {
                    localcontext.Trace(string.Format(CultureInfo.InvariantCulture,
                        "{0} should be registered against Create and Update events of Opportunity entity.", this.ChildClassName));
                    throw new InvalidPluginExecutionException(string.Format(CultureInfo.InvariantCulture,
                        "{0} should be registered against Create and Update events of Opportunity entity.", this.ChildClassName));
                }

                #endregion             //Execute the plugin logic
                RunPlugin(localcontext);
            }
            catch (Exception e)
            {
                localcontext.Trace(string.Format(CultureInfo.InvariantCulture, "Exception: {0}", e.ToString()));
                throw new InvalidPluginExecutionException(e.Message);
            }
            finally
            {
                localcontext.Trace(string.Format(CultureInfo.InvariantCulture, "Exiting {0}.Execute()", this.ChildClassName));
            }
        }

        #endregion

        #region RunPlugin
        
        private void RunPlugin(LocalPluginContext localContext)
        {
            //Protect against recursive execution of the plugin. 
            #region OptionalDepthControl

            var depth = localContext.PluginExecutionContext.Depth;
            localContext.Trace($"Execution Context Depth = {depth}");
            if (depth > 1)
            {
                return;
            }
            #endregion

            try
            {
                #region GetContextEntityAndImages

                Opportunity contextEntity = null;
                contextEntity = (localContext.PluginExecutionContext.InputParameters["Target"] as Entity).ToEntity<Opportunity>();
                localContext.Trace($"ContextEntity : {contextEntity.LogicalName} - {contextEntity.Id} - Number of Attributes = {contextEntity.Attributes.Count}");


                #endregion GetContextEntityAndImages

                #region PluginCode

                localContext.Trace("Inside RunPlugin");

                PriceLevel priceLevelEntity = GetCurrentPriceLevel(localContext, (DateTime)contextEntity.CreatedOn);
                if(priceLevelEntity == null)
                {
                    return;
                }
                else
                {
                    
                    contextEntity.PriceLevelId = new EntityReference(PriceLevel.EntityLogicalName, priceLevelEntity.Id);
                    localContext.OrganizationService.Update(contextEntity);
                    localContext.Trace("Information: PriceLevel Set");
                }
                

                #endregion PluginCode
            }
            catch (Exception exception) //Catch and throw all exception as InvalidPluginExecutionException
            {
                throw new InvalidPluginExecutionException(exception.Message, exception);
            }
        }

        #endregion

        #region ContextAndSettingsClasss
        protected class LocalPluginContext
        {
            internal IServiceProvider ServiceProvider
            {
                get;

                private set;
            }

            internal IOrganizationService OrganizationService
            {
                get;

                private set;
            }

            internal IPluginExecutionContext PluginExecutionContext
            {
                get;

                private set;
            }

            internal ITracingService TracingService
            {
                get;

                private set;
            }

            private LocalPluginContext()
            {
            }

            internal LocalPluginContext(IServiceProvider serviceProvider)
            {
                if (serviceProvider == null)
                {
                    throw new ArgumentNullException("serviceProvider");
                }

                // Obtain the execution context service from the service provider.
                this.PluginExecutionContext = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

                // Obtain the tracing service from the service provider.
                this.TracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

                // Obtain the Organization Service factory service from the service provider
                IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

                // Use the factory to generate the Organization Service.
                this.OrganizationService = factory.CreateOrganizationService(this.PluginExecutionContext.UserId);
            }

            internal void Trace(string message)
            {
                if (string.IsNullOrWhiteSpace(message) || this.TracingService == null)
                {
                    return;
                }

                if (this.PluginExecutionContext == null)
                {
                    this.TracingService.Trace(message);
                }
                else
                {
                    this.TracingService.Trace(
                        "{0}, Correlation Id: {1}, Initiating User: {2}",
                        message,
                        this.PluginExecutionContext.CorrelationId,
                        this.PluginExecutionContext.InitiatingUserId);
                }
            }
        }

        private Collection<Tuple<int, string, string, Action<LocalPluginContext>>> registeredEvents;

        protected Collection<Tuple<int, string, string, Action<LocalPluginContext>>> RegisteredEvents
        {
            get
            {
                if (this.registeredEvents == null)
                {
                    this.registeredEvents = new Collection<Tuple<int, string, string, Action<LocalPluginContext>>>();
                }

                return this.registeredEvents;
            }
        }

        #endregion

        #region Constructors 
        protected string ChildClassName
        {
            get;

            private set;
        }
        internal PostOpportunityCreateSetPriceList(Type childClassName)
        {
            this.ChildClassName = childClassName.ToString();
        }
        public PostOpportunityCreateSetPriceList() :
            this(typeof(PostOpportunityCreateSetPriceList))
        {
        }
        #endregion

        #region HelperMethods


        #region GetCurrentPriceLevel
        private PriceLevel GetCurrentPriceLevel(LocalPluginContext localContext, DateTime createdOn)
        {
            int createdOnYear = createdOn.Year;
            DateTime startDate = new DateTime(createdOnYear, 1, 1);
            DateTime endDate = new DateTime(createdOnYear, 12, 31);


            // Instantiate QueryExpression QEpricelevel
            var QEpricelevel = new QueryExpression(PriceLevel.EntityLogicalName);

            // Add columns to QEpricelevel.ColumnSet
            QEpricelevel.ColumnSet.AddColumns(PriceLevel.Fields.Name, PriceLevel.Fields.EndDate, PriceLevel.Fields.BeginDate, PriceLevel.Fields.PriceLevelId);
            QEpricelevel.AddOrder(PriceLevel.Fields.Name, OrderType.Ascending);

            // Define filter QEpricelevel.Criteria
            QEpricelevel.Criteria.AddCondition(PriceLevel.Fields.BeginDate, ConditionOperator.On, startDate);
            QEpricelevel.Criteria.AddCondition(PriceLevel.Fields.EndDate, ConditionOperator.On, endDate);

            EntityCollection pricelevelCollection = localContext.OrganizationService.RetrieveMultiple(QEpricelevel);
            if (pricelevelCollection.Entities.Count > 0)
            {
                localContext.Trace("Information: PriceList Found");
                return (PriceLevel)pricelevelCollection.Entities[0];
            }
            else
            {
                localContext.Trace("Information: Pricelist not found");
                return null;
            }
        }

        #endregion GetCurrentPriceLevel

        #endregion HelperMethods
    }
}
